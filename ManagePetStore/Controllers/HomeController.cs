using ManagePetStore.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

namespace ManagePetStore.Controllers;

public class HomeController : Controller
{
    private static readonly string[] BlockingHotelStatuses = ["Đã đặt", "Active", "Đang ở"];
    private readonly ILogger<HomeController> _logger;
    private readonly PetStoreManagementContext _context;

    private static readonly Dictionary<string, string[]> CategoryKeywordMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["food"] = ["Thức ăn", "thức ăn", "food"],
        ["toys"] = ["Đồ chơi", "đồ chơi", "toys"],
        ["accessories"] = ["Phụ kiện", "phụ kiện", "accessories"],
        ["cages"] = ["Chuồng", "Đệm", "chuồng", "đệm", "cages"],
        ["hygiene"] = ["Vệ sinh", "vệ sinh", "hygiene"],
        ["medicine"] = ["Thuốc", "Vitamin", "thuốc", "vitamin", "medicine"],
        ["spa"] = ["Spa", "chăm sóc", "Tắm", "Sấy", "Cắt", "Tỉa", "Massage", "spa"]
    };

    public HomeController(ILogger<HomeController> logger, PetStoreManagementContext context)
    {
        _logger = logger;
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? search, string? category, string? species)
    {
        var model = GetStaticHomepageData();
        model.Pets = [];
        model.RoomTypes = [];
        model.HotelFoodOptions = [];
        model.SearchKeyword = search?.Trim();
        model.SelectedCategorySlug = category?.Trim().ToLowerInvariant();
        
        var selectedSpecies = species?.Trim();
        ViewBag.SelectedSpecies = selectedSpecies;

        model.IsFiltered = !string.IsNullOrWhiteSpace(model.SearchKeyword) ||
                           !string.IsNullOrWhiteSpace(model.SelectedCategorySlug) ||
                           (!string.IsNullOrEmpty(selectedSpecies) && selectedSpecies != "Tất cả");

        ViewBag.SearchKeyword = model.SearchKeyword;

        var catalog = await GetSearchableProductsAsync();
        var filteredList = ApplyProductFilters(catalog, model.SearchKeyword, model.SelectedCategorySlug);
        
        if (!string.IsNullOrEmpty(selectedSpecies) && selectedSpecies != "Tất cả")
        {
            filteredList = filteredList.Where(p => string.Equals(p.TargetSpecies, "Tất cả", StringComparison.OrdinalIgnoreCase) 
                                                || string.Equals(p.TargetSpecies, selectedSpecies, StringComparison.OrdinalIgnoreCase))
                                       .ToList();
        }
        
        model.BestSellers = filteredList;
               
        try
        {
            model.RoomTypes = await _context.RoomTypes
                .AsNoTracking()
                .Where(r => r.Status &&
                            HotelRoomTypeCatalog.Codes.Contains(r.Code) &&
                            r.Cages.Any())
                .OrderBy(r => r.DailyPrice)
                .Select(r => new RoomTypeOptionItem
                {
                    Id = r.RoomTypeId,
                    Code = r.Code,
                    Name = r.Type,
                    Size = r.Size,
                    Capacity = r.Capacity,
                    DailyPrice = r.DailyPrice,
                    HasAc = r.HasAc,
                    HasCamera = r.HasCamera,
                    HasPremiumFood = r.HasPremiumFood
                })
                .ToListAsync();

            var reservedFoodUnits = await _context.HotelBookingFoodPlans
                .AsNoTracking()
                .Where(plan => plan.ProductSku != null &&
                               plan.InventoryQuantityDeducted == 0 &&
                               BlockingHotelStatuses.Contains(plan.HotelBooking.Status))
                .GroupBy(plan => plan.ProductSku!)
                .Select(group => new
                {
                    Sku = group.Key,
                    Quantity = group.Sum(plan => plan.ChargeableDays)
                })
                .ToDictionaryAsync(item => item.Sku, item => item.Quantity);

            var hotelFoodProducts = await _context.Products
                .AsNoTracking()
                .Where(product => !product.IsDeleted &&
                                  product.Stock > 0 &&
                                  product.Unit == HotelFoodCatalog.DailyUnit &&
                                  product.Category != null &&
                                  !product.Category.IsDeleted &&
                                  product.Category.Code == HotelFoodCatalog.CategoryCode)
                .OrderBy(product => product.Price)
                .ThenBy(product => product.Name)
                .Select(product => new HotelFoodOptionItem
                {
                    Sku = product.Sku,
                    Name = product.Name,
                    Description = product.Description ?? string.Empty,
                    TargetSpecies = product.AnimalType ?? "Tất cả",
                    PricePerDay = product.Price,
                    Unit = product.Unit,
                    Stock = product.Stock,
                    ImageUrl = product.ImageUrl
                })
                .ToListAsync();

            foreach (var product in hotelFoodProducts)
            {
                product.AvailableUnits = Math.Max(
                    0,
                    product.Stock - reservedFoodUnits.GetValueOrDefault(product.Sku));
            }

            model.HotelFoodOptions = hotelFoodProducts
                .Where(product => product.AvailableUnits > 0)
                .ToList();

            if (User.Identity?.IsAuthenticated == true)
            {
                var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
                if (userIdClaim != null && int.TryParse(userIdClaim.Value, out var userId))
                {
                    var customer = await _context.Customers
                        .AsNoTracking()
                        .FirstOrDefaultAsync(c => c.UserId == userId);

                    if (customer != null)
                    {
                        model.Pets = await _context.Pets
                            .AsNoTracking()
                            .Where(p => p.CustomerId == customer.CustomerId &&
                                        p.Status == "Active" &&
                                        p.Weight > 0)
                            .OrderBy(p => p.Name)
                            .Select(p => new PetOptionItem
                            {
                                    Id = p.PetId,
                                    Name = p.Name,
                                    Breed = p.Breed ?? p.Species,
                                    ProfileWeightKg = p.Weight
                                })
                            .ToListAsync();

                        model.HotelMembershipTier = customer.MembershipTier;
                        model.HotelDiscountPercent = ResolveHotelDiscountPercent(customer.MembershipTier);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Không thể tải dữ liệu form đặt Hotel online.");
        }

        // =========================================================================
        // Blog CMS: Tải 2 danh sách tách biệt từ database
        // =========================================================================
        try
        {
            // LatestBlogs: 4 bài mới nhất (IsFeatured ưu tiên trước, sau đó CreatedAt giảm dần)
            model.LatestBlogs = await _context.Blogs
                .Include(b => b.Author)
                .Where(b => b.IsPublished)
                .OrderByDescending(b => b.IsFeatured)
                .ThenByDescending(b => b.CreatedAt)
                .Take(4)
                .Select(b => new BlogSummaryItem
                {
                    BlogId    = b.BlogId,
                    Slug      = b.Slug,
                    Title     = b.Title,
                    CoverImage= b.CoverImage,
                    Category  = b.Category,
                    IsFeatured= b.IsFeatured,
                    ViewCount = b.ViewCount,
                    CreatedAt = b.CreatedAt,
                    AuthorName= b.Author.FullName,
                    Excerpt   = b.ContentBody.Length > 140
                                    ? b.ContentBody.Substring(0, 140)
                                    : b.ContentBody
                })
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi tải LatestBlogs từ Database.");
        }

        try
        {
            // PopularBlogs: 5 bài đọc nhiều nhất (ViewCount giảm dần)
            model.PopularBlogs = await _context.Blogs
                .Where(b => b.IsPublished)
                .OrderByDescending(b => b.ViewCount)
                .Take(5)
                .Select(b => new BlogSummaryItem
                {
                    BlogId    = b.BlogId,
                    Slug      = b.Slug,
                    Title     = b.Title,
                    CoverImage= b.CoverImage,
                    Category  = b.Category,
                    ViewCount = b.ViewCount,
                    CreatedAt = b.CreatedAt
                })
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi tải PopularBlogs từ Database.");
        }

        if (model.IsFiltered)
        {
            return View(model);
        }

        // Use the full dynamic catalog (including active DB products and Spa Services) instead of static mockup list
        model.BestSellers = catalog;
        return View(model);

    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }

    private async Task<List<ProductCardItem>> GetSearchableProductsAsync()
    {
        var products = GetStaticProductCatalog();   // khoi tao danh sach product 
        foreach (var p in products)
        {
            p.TargetSpecies = "Tất cả";
        }

        // 1. Tải các sản phẩm từ database (cách ly trong try-catch)
        try
        {
            var dbProducts = await _context.Products  //Bắt đầu truy vấn vào bảng Products trong Database. Chờ (await) đến khi lấy xong dữ liệu.
                .Include(p => p.Category)
                .OrderByDescending(p => p.Stock)   // Sắp xếp danh sách giảm dần theo số lượng tồn kho
                .ToListAsync();  // Thực thi câu lệnh SQL và ép kết quả ra thành một List trong C#.

            foreach (var p in dbProducts)
            {
                if (products.Any(x => x.Sku.Equals(p.Sku, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                products.Add(MapDbProduct(p));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi tải Products từ Database.");
        }

        // 2. Tải các dịch vụ Spa từ database (cách ly hoàn toàn trong try-catch và chỉ lấy Active == true)
        try
        {
            var dbSpaServices = await _context.SpaServices   // Truy vấn vào bảng SpaServices.
                .Where(s => s.Active)   // filter san pham , chỉ lấy các dịch vụ đang ở trạng thái hoạt động (Active == true).
                .OrderBy(s => s.ServiceId)  //Sắp xếp tăng dần theo ID dịch vụ và đẩy ra thành List.
                .ToListAsync();

            var seenSpaNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var s in dbSpaServices)
            {
                var normalizedName = s.Name.Trim();
                if (!seenSpaNames.Add(normalizedName))  // trung dich vu , bo qua 
                {
                    continue;
                }

                var sku = $"SPA-SVC-{s.ServiceId:D3}";   // sku ảo 
                if (products.Any(x => x.Sku.Equals(sku, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                products.Add(new ProductCardItem
                {
                    Sku = sku,
                    Name = s.Name,
                    Category = "Dịch vụ Spa",
                    Price = s.Price,
                    ImageUrl = ResolveSpaServiceImageUrl(s.Name),
                    Rating = 4.9,
                    ReviewCount = 35,
                    Badge = $"{s.DurationMinutes} phút",
                    BadgeType = "new",
                    InStock = true,
                    TargetSpecies = string.IsNullOrEmpty(s.TargetSpecies) ? "Tất cả" : s.TargetSpecies.Trim()
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi tải SpaServices từ Database.");
        }

        return products;
    }

    private static List<ProductCardItem> ApplyProductFilters(
        List<ProductCardItem> products,
        string? search,
        string? categorySlug)
    {
        IEnumerable<ProductCardItem> result = products;

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            result = result.Where(p =>
                p.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                p.Sku.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(categorySlug) &&
            CategoryKeywordMap.TryGetValue(categorySlug, out var keywords))  // tra cuu du lieu trong dictionary 
        {
            result = result.Where(p =>
                keywords.Any(k => p.Category.Contains(k, StringComparison.OrdinalIgnoreCase)));
        }

        return result.OrderBy(p => p.Name).ToList();
    }

    private static ProductCardItem MapDbProduct(Product product)
    {
        return new ProductCardItem
        {
            Sku = product.Sku,
            Name = product.Name,
            Category = product.Category?.Name ?? "PETSTORE",
            Price = product.Price,
            ImageUrl = ResolveProductImageUrl(product),
            Rating = 4.7,
            ReviewCount = 50,
            InStock = product.Stock > 0,
            TargetSpecies = "Tất cả"
        };
    }

    private static int ResolveHotelDiscountPercent(string? membershipTier)
    {
        return membershipTier?.Trim().ToLowerInvariant() switch
        {
            "gold" or "vàng" => 10,
            "silver" or "bạc" => 5,
            _ => 0
        };
    }

    private static string ResolveSpaServiceImageUrl(string serviceName)
    {
        var name = serviceName.ToLowerInvariant();

        if (name.Contains("tắm") || name.Contains("tam") || name.Contains("sấy") || name.Contains("say") || name.Contains("chải"))
        {
            return "/images/spa-bath-dry.png";
        }

        if (name.Contains("cắt") || name.Contains("cat") || name.Contains("tỉa") || name.Contains("tia") || name.Contains("grooming") || name.Contains("kiểu"))
        {
            return "/images/spa-grooming.png";
        }

        if (name.Contains("răng") || name.Contains("rang") || name.Contains("cao") || name.Contains("miệng") || name.Contains("mieng"))
        {
            return "/images/spa-dental.png";
        }

        if (name.Contains("combo") || name.Contains("vip") || name.Contains("toàn diện") || name.Contains("toan dien"))
        {
            return "/images/spa-vip.png";
        }

        return "/images/spa-bath-dry.png";
    }

    private static string ResolveProductImageUrl(Product product)
    {
        var url = product.ImageUrl?.Trim();
        if (!string.IsNullOrEmpty(url) &&
            (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
             url.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
             url.StartsWith('/')))
        {
            return url;
        }
        //Nếu sản phẩm không có ảnh hợp lệ, hệ thống sẽ "đoán" xem nó thuộc loại gì để gán ảnh.
        if (product.Sku.Equals("PROD-ROYAL-01", StringComparison.OrdinalIgnoreCase) ||
            (product.Category != null && product.Category.Name.Contains("Thức ăn", StringComparison.OrdinalIgnoreCase)) ||
            (product.Category != null && product.Category.Name.Contains("Thuc an", StringComparison.OrdinalIgnoreCase)))
        {
            return "https://images.unsplash.com/photo-1589924691995-400dc9ecc119?w=400&h=400&fit=crop";
        }

        return "https://images.unsplash.com/photo-1589924691995-400dc9ecc119?w=400&h=400&fit=crop";
    }


    //Khi Database trống rỗng hoặc bị lỗi kết nối, hàm này sẽ tung ra một danh sách sản phẩm mẫu để giao diện luôn có nội dung.
    private static List<ProductCardItem> GetStaticProductCatalog()
    {
        return GetStaticHomepageData().BestSellers
            .Select(p => new ProductCardItem  // Sử dụng LINQ Select để duyệt qua từng phần tử p trong mảng dữ liệu gốc.
            {
                Sku = p.Sku,
                Name = p.Name,
                Category = p.Category,
                Price = p.Price,
                OriginalPrice = p.OriginalPrice,
                ImageUrl = p.ImageUrl,
                Rating = p.Rating,
                ReviewCount = p.ReviewCount,
                Badge = p.Badge,
                BadgeType = p.BadgeType,
                InStock = p.InStock
            })
            .ToList();
    }

    private static HomepageViewModel GetStaticHomepageData()
    {
        return new HomepageViewModel
        {
            Categories =
            [
                new CategoryItem { Name = "Thức ăn", Icon = "bi-basket2", Slug = "food" },
                new CategoryItem { Name = "Đồ chơi", Icon = "bi-balloon", Slug = "toys" },
                new CategoryItem { Name = "Phụ kiện", Icon = "bi-gift", Slug = "accessories" },
                new CategoryItem { Name = "Chuồng & Đệm", Icon = "bi-house-door", Slug = "cages" },
                new CategoryItem { Name = "Vệ sinh", Icon = "bi-droplet", Slug = "hygiene" },
                new CategoryItem { Name = "Thuốc & Vitamin", Icon = "bi-capsule", Slug = "medicine" },
                new CategoryItem { Name = "Dịch vụ Spa", Icon = "bi-scissors", Slug = "spa" },
                new CategoryItem { Name = "Sổ y tế thú cưng", Icon = "bi-journal-medical", Slug = "medical-records" }
            ],
            BestSellers =
            [
                new ProductCardItem
                {
                    Sku = "RC-MBC-001",
                    Name = "Royal Canin Mother & Babycat",
                    Category = "Thức ăn",
                    Price = 350000,
                    OriginalPrice = 388000,
                    ImageUrl = "https://images.unsplash.com/photo-1589924691995-400dc9ecc119?w=400&h=400&fit=crop",
                    Rating = 4.8,
                    ReviewCount = 124,
                    Badge = "-10%",
                    BadgeType = "discount",
                    InStock = true
                },
                new ProductCardItem
                {
                    Sku = "MN-CAT-5L",
                    Name = "Cát vệ sinh Maneki Neko 5L",
                    Category = "Vệ sinh",
                    Price = 89000,
                    ImageUrl = "https://images.unsplash.com/photo-1574158622682-e40e69881006?w=400&h=400&fit=crop",
                    Rating = 4.6,
                    ReviewCount = 89,
                    InStock = true
                },
                new ProductCardItem
                {
                    Sku = "JD-SHAMPOO",
                    Name = "Sữa tắm Joyce & Dolls 400ml",
                    Category = "Vệ sinh",
                    Price = 125000,
                    ImageUrl = "https://images.unsplash.com/photo-1516734212186-a967f81ad0d7?w=400&h=400&fit=crop",
                    Rating = 4.9,
                    ReviewCount = 56,
                    Badge = "Mới",
                    BadgeType = "new",
                    InStock = true
                },
                new ProductCardItem
                {
                    Sku = "BONE-TET-5",
                    Name = "Xương gặm cho chó 5 cây",
                    Category = "Thức ăn",
                    Price = 45000,
                    ImageUrl = "/images/dog-bone-chew.png",
                    Rating = 4.5,
                    ReviewCount = 32,
                    InStock = true
                }
            ],
            FeaturedBlog = new FeaturedBlogItem
            {
                Title = "10 Bí quyết giúp thú cưng của bạn luôn khỏe mạnh và hạnh phúc",
                Excerpt = "Chăm sóc thú cưng không chỉ là việc cho ăn. Hãy cùng khám phá những thói quen tốt để cải thiện chất lượng sống cho người bạn nhỏ của mình.",
                ImageUrl = "https://images.unsplash.com/photo-1450778869180-41d0601e046e?w=1200&h=500&fit=crop"
            },
            BlogArticles =
            [
                new BlogCardItem
                {
                    Id = 1,
                    Title = "Chế độ dinh dưỡng hoàn hảo cho chó con dưới ...",
                    Excerpt = "Tìm hiểu về các loại dưỡng chất thiết yếu giúp cún cưng phát triển xương và cơ bắp toàn diện.",
                    Tag = "#DINH DƯỠNG",
                    ImageUrl = "https://images.unsplash.com/photo-1601758228041-f3b2795255f1?w=600&h=350&fit=crop"
                },
                new BlogCardItem
                {
                    Id = 2,
                    Title = "Cách xử lý tình trạng mèo bị rụng lông quá...",
                    Excerpt = "Rụng lông là vấn đề phổ biến ở mèo, nhưng rụng quá nhiều có thể là dấu hiệu của sức khỏe kém.",
                    Tag = "#SỨC KHỎE",
                    ImageUrl = "https://images.unsplash.com/photo-1516734212186-a967f81ad0d7?w=600&h=350&fit=crop"
                },
                new BlogCardItem
                {
                    Id = 3,
                    Title = "Hướng dẫn huấn luyện chó cơ bản tại nhà cực...",
                    Excerpt = "Chỉ với 15 phút mỗi ngày, bạn có thể dạy cún cưng những lệnh cơ bản như ngồi, nằm và bắt tay.",
                    Tag = "#LÀM ĐẸP",
                    ImageUrl = "https://images.unsplash.com/photo-1558788353-f76d92427f16?w=600&h=350&fit=crop"
                }
            ],
            Pets =
            [
                new PetOptionItem { Id = 1, Name = "LuLu", Breed = "Poodle" }
            ],
            RoomTypes =
            [
                new RoomTypeOptionItem { Id = 2, Code = HotelRoomTypeCatalog.StandardCode, Name = "Phòng Standard", Size = "1.0m x 1.0m", Capacity = 1, DailyPrice = 200000, HasAc = true },
                new RoomTypeOptionItem { Id = 1, Code = HotelRoomTypeCatalog.VipCode, Name = "Phòng VIP", Size = "1.5m x 1.5m", Capacity = 1, DailyPrice = 500000, HasAc = true, HasCamera = true },
                new RoomTypeOptionItem { Id = 3, Code = HotelRoomTypeCatalog.LuxuryCode, Name = "Phòng Luxury", Size = "2.0m x 1.8m", Capacity = 1, DailyPrice = 750000, HasAc = true, HasCamera = true }
            ]
        };
    }
}
