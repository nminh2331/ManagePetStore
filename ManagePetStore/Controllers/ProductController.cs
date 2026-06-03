using ManagePetStore.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ManagePetStore.Controllers;

public class ProductController : Controller
{
    private readonly PetStoreManagementContext _context;

    public ProductController(PetStoreManagementContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> Details(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return RedirectToAction("Index", "Home");
        }

        ProductDetailViewModel? model = null;  // Khai báo biến model có kiểu dữ liệu là ProductDetailViewModel

        try
        {
            if (id.StartsWith("SPA-SVC-", StringComparison.OrdinalIgnoreCase))
            {
                var idString = id.Substring(8); //Cắt bỏ 8 ký tự đầu tiên (SPA-SVC-) để lấy phần số ở đuôi (ví dụ 001).
                if (int.TryParse(idString, out int serviceId))  // Ép phần đuôi đó thành số nguyên (int).
                {
                    var spaService = await _context.SpaServices.FirstOrDefaultAsync(s => s.ServiceId == serviceId);
                    if (spaService != null)
                    {
                        model = MapFromSpaService(spaService);  // chuyển đổi dữ liệu thô từ DB thành ProductDetailViewModel để UI đọc được.
                    }
                }
            }
            else
            {
                var product = await _context.Products.Include(p => p.Category).FirstOrDefaultAsync(p => p.Sku == id);  
                //Eager Loading để lấy kèm thông tin Danh mục (tương tự trang chủ).
                if (product != null)
                {
                    model = MapFromProduct(product);
                }
            }
        }
        catch
        {
            // Fallback to static demo data below.
        }

        model ??= GetStaticProduct(id) ?? GetStaticProduct("RC-MBC-001")!;

        return View(model);
    }

    private static ProductDetailViewModel MapFromSpaService(SpaService service)
    {
        var originalPrice = Math.Round(service.Price * 1.11m, 0);
        var discount = (int)Math.Round((1 - service.Price / originalPrice) * 100);

        return new ProductDetailViewModel
        {
            Sku = $"SPA-SVC-{service.ServiceId:D3}",
            Brand = "DỊCH VỤ SPA",
            Name = service.Name,
            FullTitle = $"{service.Name} - Liệu trình chăm sóc chuyên nghiệp",
            Price = service.Price,
            OriginalPrice = originalPrice,
            DiscountPercent = discount,
            Savings = originalPrice - service.Price,
            Rating = 4.9,
            ReviewCount = 35,
            SoldCount = "100+",
            Description = $"Dịch vụ {service.Name} chất lượng cao giúp thú cưng sạch sẽ, khỏe mạnh và thoải mái. Liệu trình thực hiện trong {service.DurationMinutes} phút bởi các chuyên viên spa tay nghề cao.",
            Stock = 999, // Spa services always have virtual stock
            InStock = true,
            Images =
            [
                "https://images.unsplash.com/photo-1516734212186-a967f81ad0d7?w=600&h=600&fit=crop",
                "https://images.unsplash.com/photo-1558788353-f76d92427f16?w=600&h=600&fit=crop",
                "https://images.unsplash.com/photo-1548199973-03cce0bbc87b?w=600&h=600&fit=crop"
            ],
            Features =
            [
                $"Thời gian thực hiện: {service.DurationMinutes} phút",
                "Chăm sóc tận tình chuẩn 5 sao",
                "Chuyên viên giàu kinh nghiệm",
                "Sử dụng sữa tắm, mỹ phẩm cao cấp an toàn"
            ]
        };
    }


    private static ProductDetailViewModel MapFromProduct(Product product)
    {
        var originalPrice = Math.Round(product.Price * 1.11m, 0);
        var discount = (int)Math.Round((1 - product.Price / originalPrice) * 100);

        return new ProductDetailViewModel
        {
            Sku = product.Sku,
            Brand = product.Category?.Name.ToUpperInvariant() ?? "PETSTORE",
            Name = product.Name,
            FullTitle = product.Name,
            Price = product.Price,
            OriginalPrice = originalPrice,
            DiscountPercent = discount,
            Savings = originalPrice - product.Price,
            Rating = 4.8,
            ReviewCount = 124,
            SoldCount = "1.2k+",
            Description = "Thức ăn cao cấp được nghiên cứu đặc biệt cho mèo mẹ và mèo con, cung cấp đầy đủ dưỡng chất thiết yếu giúp mèo con phát triển khỏe mạnh.",
            Stock = product.Stock,
            InStock = product.Stock > 0,
            Images =
            [
                string.IsNullOrEmpty(product.ImageUrl)
                    ? "https://images.unsplash.com/photo-1589924691995-400dc9ecc119?w=600&h=600&fit=crop"
                    : product.ImageUrl,
                "https://images.unsplash.com/photo-1516734212186-a967f81ad0d7?w=600&h=600&fit=crop",
                "https://images.unsplash.com/photo-1450778869180-41d0601e046e?w=600&h=600&fit=crop"
            ],
            Features =
            [
                "Hỗ trợ hệ miễn dịch",
                "Dễ dàng cai sữa",
                "Tăng cường sức khỏe hệ tiêu hóa",
                "Giàu DHA cho phát triển trí não"
            ]
        };
    }

    private static ProductDetailViewModel? GetStaticProduct(string sku)
    {
        var products = new Dictionary<string, ProductDetailViewModel>(StringComparer.OrdinalIgnoreCase)
        {
            ["RC-MBC-001"] = new()
            {
                Sku = "RC-MBC-001",
                Brand = "ROYAL CANIN",
                Name = "Royal Canin Mother & Babycat",
                FullTitle = "Royal Canin Mother & Babycat - Thức ăn cho mèo mẹ và mèo con",
                Price = 350000,
                OriginalPrice = 388000,
                DiscountPercent = 10,
                Savings = 38000,
                Rating = 4.8,
                ReviewCount = 124,
                SoldCount = "1.2k+",
                Description = "Thức ăn cao cấp được nghiên cứu đặc biệt cho mèo mẹ và mèo con, cung cấp đầy đủ dưỡng chất thiết yếu giúp mèo con phát triển khỏe mạnh trong giai đoạn đầu đời quan trọng nhất.",
                Stock = 45,
                InStock = true,
                Images =
                [
                    "https://images.unsplash.com/photo-1589924691995-400dc9ecc119?w=600&h=600&fit=crop",
                    "https://images.unsplash.com/photo-1516734212186-a967f81ad0d7?w=600&h=600&fit=crop",
                    "https://images.unsplash.com/photo-1450778869180-41d0601e046e?w=600&h=600&fit=crop"
                ],
                Features =
                [
                    "Hỗ trợ hệ miễn dịch",
                    "Dễ dàng cai sữa",
                    "Tăng cường sức khỏe hệ tiêu hóa",
                    "Giàu DHA cho phát triển trí não"
                ]
            },
            ["MN-CAT-5L"] = new()
            {
                Sku = "MN-CAT-5L",
                Brand = "MANEKI NEKO",
                Name = "Cát vệ sinh Maneki Neko 5L",
                FullTitle = "Cát vệ sinh Maneki Neko 5L - Khử mùi hiệu quả",
                Price = 89000,
                OriginalPrice = 99000,
                DiscountPercent = 10,
                Savings = 10000,
                Rating = 4.6,
                ReviewCount = 89,
                SoldCount = "800+",
                Description = "Cát vệ sinh cao cấp với khả năng khử mùi vượt trội, vón cục tốt và an toàn cho mèo cưng.",
                Stock = 120,
                InStock = true,
                Images =
                [
                    "https://images.unsplash.com/photo-1516734212186-a967f81ad0d7?w=600&h=600&fit=crop",
                    "https://images.unsplash.com/photo-1589924691995-400dc9ecc119?w=600&h=600&fit=crop"
                ],
                Features = ["Khử mùi 24h", "Vón cục chắc", "Không bụi", "An toàn cho mèo"]
            },
            ["JD-SHAMPOO"] = new()
            {
                Sku = "JD-SHAMPOO",
                Brand = "JOYCE & DOLLS",
                Name = "Sữa tắm Joyce & Dolls 400ml",
                FullTitle = "Sữa tắm Joyce & Dolls 400ml - Dịu nhẹ cho da lông",
                Price = 125000,
                OriginalPrice = 125000,
                DiscountPercent = 0,
                Savings = 0,
                Rating = 4.9,
                ReviewCount = 56,
                SoldCount = "350+",
                Description = "Sữa tắm dịu nhẹ chiết xuất thảo mộc, giúp lông mềm mượt và da khỏe mạnh.",
                Stock = 60,
                InStock = true,
                Images =
                [
                    "https://images.unsplash.com/photo-1558788353-f76d92427f16?w=600&h=600&fit=crop",
                    "https://images.unsplash.com/photo-1516734212186-a967f81ad0d7?w=600&h=600&fit=crop"
                ],
                Features = ["pH cân bằng", "Không gây kích ứng", "Hương thảo mộc", "Dưỡng lông mềm"]
            },
            ["BONE-TET-5"] = new()
            {
                Sku = "BONE-TET-5",
                Brand = "PETSTORE",
                Name = "Xương gặm cho chó 5 cây",
                FullTitle = "Xương gặm cho chó 5 cây - Giúp sạch răng",
                Price = 45000,
                OriginalPrice = 50000,
                DiscountPercent = 10,
                Savings = 5000,
                Rating = 4.5,
                ReviewCount = 32,
                SoldCount = "200+",
                Description = "Xương gặm tự nhiên giúp làm sạch răng và massage nướu cho chó cưng.",
                Stock = 200,
                InStock = true,
                Images =
                [
                    "/images/dog-bone-chew.png"
                ],
                Features = ["Làm sạch răng", "Massage nướu", "100% tự nhiên", "Không chất bảo quản"]
            }
        };

        return products.GetValueOrDefault(sku);
    }
}
