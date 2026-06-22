using ManagePetStore.Areas.Customer.Models;
using ManagePetStore.Models;
using Microsoft.EntityFrameworkCore;

namespace ManagePetStore.Areas.Customer.Services;

public class CartProductResolver
{
    private readonly PetStoreManagementContext _context;

    public CartProductResolver(PetStoreManagementContext context)
    {
        _context = context;
    }

    public async Task<CartProductInfo?> ResolveAsync(string sku)
    {
        if (string.IsNullOrWhiteSpace(sku))
        {
            return null;
        }

        try
        {
            if (sku.StartsWith("SPA-SVC-", StringComparison.OrdinalIgnoreCase))
            {
                var idString = sku.Substring(8);
                if (int.TryParse(idString, out int serviceId))
                {
                    var spaService = await _context.SpaServices.FirstOrDefaultAsync(s => s.ServiceId == serviceId);
                    if (spaService != null)
                    {
                        return new CartProductInfo
                        {
                            Sku = sku,
                            Name = spaService.Name,
                            Price = spaService.Price,
                            Stock = 999, // Virtual stock
                            ImageUrl = "https://images.unsplash.com/photo-1516734212186-a967f81ad0d7?w=200&h=200&fit=crop"
                        };
                    }
                }
            }
            else
            {
                var product = await _context.Products.FirstOrDefaultAsync(p => p.Sku == sku);
                if (product != null)
                {
                    return MapFromProduct(product);
                }
            }
        }
        catch
        {
            // Fallback to static catalog below.
        }

        return GetStaticProduct(sku);
    }

    private static CartProductInfo MapFromProduct(Product product)
    {
        return new CartProductInfo
        {
            Sku = product.Sku,
            Name = product.Name,
            Price = product.Price,
            Stock = product.Stock,
            ImageUrl = string.IsNullOrEmpty(product.ImageUrl)
                ? "https://images.unsplash.com/photo-1589924691995-400dc9ecc119?w=200&h=200&fit=crop"
                : product.ImageUrl
        };
    }

    private static CartProductInfo? GetStaticProduct(string sku)
    {
        var products = new Dictionary<string, CartProductInfo>(StringComparer.OrdinalIgnoreCase)
        {
            ["RC-MBC-001"] = new()
            {
                Sku = "RC-MBC-001",
                Name = "Royal Canin Mother & Babycat",
                Price = 350000,
                Stock = 45,
                ImageUrl = "https://images.unsplash.com/photo-1589924691995-400dc9ecc119?w=200&h=200&fit=crop"
            },
            ["MN-CAT-5L"] = new()
            {
                Sku = "MN-CAT-5L",
                Name = "Cát vệ sinh Maneki Neko 5L",
                Price = 89000,
                Stock = 120,
                ImageUrl = "https://images.unsplash.com/photo-1516734212186-a967f81ad0d7?w=200&h=200&fit=crop"
            },
            ["JD-SHAMPOO"] = new()
            {
                Sku = "JD-SHAMPOO",
                Name = "Sữa tắm Joyce & Dolls 400ml",
                Price = 125000,
                Stock = 60,
                ImageUrl = "https://images.unsplash.com/photo-1558788353-f76d92427f16?w=200&h=200&fit=crop"
            },
            ["BONE-TET-5"] = new()
            {
                Sku = "BONE-TET-5",
                Name = "Xương gặm cho chó 5 cây",
                Price = 45000,
                Stock = 200,
                ImageUrl = "/images/dog-bone-chew.png"
            }
        };

        return products.GetValueOrDefault(sku);
    }
}
