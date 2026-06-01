using System.Text.Json;
using ManagePetStore.Areas.Customer.Models;
using ManagePetStore.Models;
using Microsoft.EntityFrameworkCore;

namespace ManagePetStore.Areas.Customer.Services;

public class CartService : ICartService
{
    private const string CartSessionKey = "ShoppingCart";
    private const string VoucherSessionKey = "AppliedVoucher";

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly CartProductResolver _productResolver;
    private readonly PetStoreManagementContext _context;

    public CartService(
        IHttpContextAccessor httpContextAccessor,
        CartProductResolver productResolver,
        PetStoreManagementContext context)
    {
        _httpContextAccessor = httpContextAccessor;
        _productResolver = productResolver;
        _context = context;
    }

    public async Task<CartPageViewModel> GetCartPageAsync()
    {
        var items = GetCartItems();
        var viewModel = new CartPageViewModel();

        foreach (var item in items)
        {
            var product = await _productResolver.ResolveAsync(item.Sku);
            if (product == null)
            {
                continue;
            }

            var quantity = Math.Min(item.Quantity, product.Stock);
            if (quantity <= 0)
            {
                continue;
            }

            viewModel.Items.Add(new CartLineItemViewModel
            {
                Sku = product.Sku,
                Name = product.Name,
                ImageUrl = product.ImageUrl,
                UnitPrice = product.Price,
                Quantity = quantity,
                MaxStock = product.Stock
            });
        }

        SaveCartItems(viewModel.Items.Select(i => new CartSessionItem
        {
            Sku = i.Sku,
            Name = i.Name,
            Price = i.UnitPrice,
            ImageUrl = i.ImageUrl,
            Quantity = i.Quantity,
            MaxStock = i.MaxStock
        }).ToList());

        var appliedVoucher = GetAppliedVoucher();
        if (appliedVoucher != null)
        {
            var discount = await CalculateVoucherDiscountAsync(appliedVoucher.Code, viewModel.Subtotal);
            if (discount > 0)
            {
                viewModel.VoucherDiscount = discount;
                viewModel.AppliedVoucherCode = appliedVoucher.Code;
            }
            else
            {
                ClearVoucher();
            }
        }

        return viewModel;
    }

    public int GetTotalQuantity()
    {
        return GetCartItems().Sum(i => i.Quantity);
    }

    public async Task<(bool Success, string Message)> AddItemAsync(string sku, int quantity)
    {
        if (quantity < 1)
        {
            return (false, "Số lượng không hợp lệ.");
        }

        var product = await _productResolver.ResolveAsync(sku);
        if (product == null)
        {
            return (false, "Không tìm thấy sản phẩm.");
        }

        if (!product.InStock)
        {
            return (false, "Sản phẩm đã hết hàng.");
        }

        var items = GetCartItems();
        var existing = items.FirstOrDefault(i => i.Sku.Equals(product.Sku, StringComparison.OrdinalIgnoreCase));

        if (existing != null)
        {
            var newQty = Math.Min(existing.Quantity + quantity, product.Stock);
            if (newQty == existing.Quantity)
            {
                return (false, $"Chỉ còn {product.Stock} sản phẩm trong kho.");
            }

            existing.Quantity = newQty;
            existing.Price = product.Price;
            existing.Name = product.Name;
            existing.ImageUrl = product.ImageUrl;
            existing.MaxStock = product.Stock;
        }
        else
        {
            items.Add(new CartSessionItem
            {
                Sku = product.Sku,
                Name = product.Name,
                Price = product.Price,
                ImageUrl = product.ImageUrl,
                Quantity = Math.Min(quantity, product.Stock),
                MaxStock = product.Stock
            });
        }

        SaveCartItems(items);
        return (true, "Đã thêm sản phẩm vào giỏ hàng.");
    }

    public async Task<(bool Success, string Message)> SetQuantityAsync(string sku, int quantity)
    {
        if (quantity < 1)
        {
            return await RemoveItemAsync(sku);
        }

        var product = await _productResolver.ResolveAsync(sku);
        if (product == null)
        {
            return (false, "Không tìm thấy sản phẩm.");
        }

        var items = GetCartItems();
        var existing = items.FirstOrDefault(i => i.Sku.Equals(sku, StringComparison.OrdinalIgnoreCase));
        if (existing == null)
        {
            return (false, "Sản phẩm không có trong giỏ hàng.");
        }

        existing.Quantity = Math.Min(quantity, product.Stock);
        existing.MaxStock = product.Stock;
        existing.Price = product.Price;
        SaveCartItems(items);

        return (true, "Đã cập nhật số lượng.");
    }

    public async Task<(bool Success, string Message)> IncreaseQuantityAsync(string sku)
    {
        var items = GetCartItems();
        var existing = items.FirstOrDefault(i => i.Sku.Equals(sku, StringComparison.OrdinalIgnoreCase));
        if (existing == null)
        {
            return (false, "Sản phẩm không có trong giỏ hàng.");
        }

        return await SetQuantityAsync(sku, existing.Quantity + 1);
    }

    public async Task<(bool Success, string Message)> DecreaseQuantityAsync(string sku)
    {
        var items = GetCartItems();
        var existing = items.FirstOrDefault(i => i.Sku.Equals(sku, StringComparison.OrdinalIgnoreCase));
        if (existing == null)
        {
            return (false, "Sản phẩm không có trong giỏ hàng.");
        }

        if (existing.Quantity <= 1)
        {
            return await RemoveItemAsync(sku);
        }

        return await SetQuantityAsync(sku, existing.Quantity - 1);
    }

    public Task<(bool Success, string Message)> RemoveItemAsync(string sku)
    {
        var items = GetCartItems();
        items.RemoveAll(i => i.Sku.Equals(sku, StringComparison.OrdinalIgnoreCase));
        SaveCartItems(items);
        return Task.FromResult((true, "Đã xóa sản phẩm khỏi giỏ hàng."));
    }

    public async Task<(bool Success, string Message)> ApplyVoucherAsync(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return (false, "Vui lòng nhập mã giảm giá.");
        }

        var cart = await GetCartPageAsync();
        if (!cart.Items.Any())
        {
            return (false, "Giỏ hàng trống, không thể áp dụng voucher.");
        }

        var trimmedCode = code.Trim().ToUpperInvariant();
        var discount = await CalculateVoucherDiscountAsync(trimmedCode, cart.Subtotal);
        if (discount <= 0)
        {
            return (false, "Mã giảm giá không hợp lệ hoặc không đủ điều kiện áp dụng.");
        }

        SaveAppliedVoucher(new AppliedVoucherSession
        {
            Code = trimmedCode,
            Discount = discount
        });

        return (true, $"Đã áp dụng mã {trimmedCode}. Giảm {discount:N0}đ.");
    }

    public void ClearVoucher()
    {
        _httpContextAccessor.HttpContext?.Session.Remove(VoucherSessionKey);
    }

    public void ClearCart()
    {
        _httpContextAccessor.HttpContext?.Session.Remove(CartSessionKey);
        ClearVoucher();
    }

    private async Task<decimal> CalculateVoucherDiscountAsync(string code, decimal subtotal)
    {
        try
        {
            var voucher = await _context.Vouchers
                .FirstOrDefaultAsync(v => v.Code == code && v.Status && v.ExpiryDate >= DateTime.Today);

            if (voucher != null && subtotal >= voucher.MinOrder)
            {
                return voucher.Type.Equals("Percent", StringComparison.OrdinalIgnoreCase)
                    ? Math.Round(subtotal * voucher.Value / 100m, 0)
                    : voucher.Value;
            }
        }
        catch
        {
            // Fallback to demo vouchers below.
        }

        return code switch
        {
            "PET20" or "SALE20" when subtotal >= 200000 => 20000m,
            "PET10" when subtotal >= 100000 => Math.Round(subtotal * 0.1m, 0),
            _ => 0m
        };
    }

    private AppliedVoucherSession? GetAppliedVoucher()
    {
        var session = _httpContextAccessor.HttpContext?.Session;
        if (session == null)
        {
            return null;
        }

        var json = session.GetString(VoucherSessionKey);
        if (string.IsNullOrEmpty(json))
        {
            return null;
        }

        return JsonSerializer.Deserialize<AppliedVoucherSession>(json);
    }

    private void SaveAppliedVoucher(AppliedVoucherSession voucher)
    {
        var session = _httpContextAccessor.HttpContext?.Session;
        if (session == null)
        {
            return;
        }

        session.SetString(VoucherSessionKey, JsonSerializer.Serialize(voucher));
    }

    private List<CartSessionItem> GetCartItems()
    {
        var session = _httpContextAccessor.HttpContext?.Session;
        if (session == null)
        {
            return [];
        }

        var json = session.GetString(CartSessionKey);
        if (string.IsNullOrEmpty(json))
        {
            return [];
        }

        return JsonSerializer.Deserialize<List<CartSessionItem>>(json) ?? [];
    }

    private void SaveCartItems(List<CartSessionItem> items)
    {
        var session = _httpContextAccessor.HttpContext?.Session;
        if (session == null)
        {
            return;
        }

        if (items.Count == 0)
        {
            session.Remove(CartSessionKey);
            return;
        }

        var json = JsonSerializer.Serialize(items);
        session.SetString(CartSessionKey, json);
    }
}
