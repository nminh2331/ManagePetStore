using ManagePetStore.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

namespace ManagePetStore.Controllers;

public class HomeController : Controller
{
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
    public async Task<IActionResult> Index(string? search, string? category)
    {
        var model = GetStaticHomepageData();
        model.SearchKeyword = search?.Trim();
        model.SelectedCategorySlug = category?.Trim().ToLowerInvariant();
        model.IsFiltered = !string.IsNullOrWhiteSpace(model.SearchKeyword) ||
                           !string.IsNullOrWhiteSpace(model.SelectedCategorySlug);

        ViewBag.SearchKeyword = model.SearchKeyword;

        var catalog = await GetSearchableProductsAsync();
        model.BestSellers = ApplyProductFilters(catalog, model.SearchKeyword, model.SelectedCategorySlug);
               
        try
        {
            //  Logic xác thực & Trích xuất dữ liệu Cá nhân (Form Đặt Khách sạn)
            if (User.Identity?.IsAuthenticated == true)
            {
                var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
                if (userIdClaim != null && int.TryParse(userIdClaim.Value, out var userId))
                {
                    var customer = await _context.Customers.FirstOrDefaultAsync(c => c.UserId == userId);
                    if (customer != null)
                    {
                        var pets = await _context.Pets
                            .Where(p => p.CustomerId == customer.CustomerId)
                            .ToListAsync();

                        if (pets.Count > 0)
                        {
                            model.Pets = pets.Select(p => new PetOptionItem
                            {
                                Id = p.PetId,
                                Name = p.Name,
                                Breed = p.Breed ?? p.Species
                            }).ToList();
                        }
                    }
                }
            }
        }
        catch
        {
            // Giữ dữ liệu mockup mặc định cho homepage.
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
                    InStock = true
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
            InStock = product.Stock > 0
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
                new CategoryItem { Name = "Dịch vụ Spa", Icon = "bi-scissors", Slug = "spa" }
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
                new RoomTypeOptionItem { Id = 1, Name = "Phòng VIP Luxury (500k/n)", DailyPrice = 500000 },
                new RoomTypeOptionItem { Id = 2, Name = "Phòng Tiêu Chuẩn (300k/n)", DailyPrice = 300000 },
                new RoomTypeOptionItem { Id = 3, Name = "Phòng Economy (200k/n)", DailyPrice = 200000 }
            ]
        };
    }
}
