
// HÀ HOÀNG HIỆP CODE -- xử lý cái manage cart 

using ManagePetStore.Areas.Customer.Models;
using ManagePetStore.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using static System.Collections.Specialized.BitVector32;

namespace ManagePetStore.Services.Customer;

public class CartService : ICartService
{
    private const string CartSessionKey = "ShoppingCart";  //Session key dùng để lưu cart
    private const string VoucherSessionKey = "AppliedVoucher"; // Session key dùng để lưu voucher.

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

    /// <summary>
    /// LUỒNG MANAGE CART: Lấy dữ liệu toàn bộ trang giỏ hàng
    /// - Kiểm tra từng sản phẩm còn trong DB/Tồn kho không.
    /// - Tự động điều chỉnh số lượng nếu vượt quá tồn kho (MaxStock).
    /// - Tính toán Voucher giảm giá nếu đã được áp dụng trước đó.
    /// </summary>
    /// 

    /// Phương thức GetCartPageAsync() - Đọc & Validate Tồn kho thực tế:
    public async Task<CartPageViewModel> GetCartPageAsync()
    {
        var items = GetCartItems();
        var viewModel = new CartPageViewModel();

        foreach (var item in items)
        {
            //Đọc thông tin sản phẩm từ CSDL qua _productResolver.
            var product = await _productResolver.ResolveAsync(item.Sku);  
            if (product == null)
            {
                continue;
            }

            // RÀNG BUỘC TỒN KHO: Số lượng trong giỏ không được vượt quá số tồn thực tế trong kho
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


        // Xử lý áp dụng Voucher và tính toán số tiền giảm giá
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

    /// <summary>
    /// LUỒNG MANAGE CART: Thêm sản phẩm vào giỏ hàng
    /// - VALIDATION 1: Số lượng yêu cầu thêm phải >= 1.
    /// - VALIDATION 2: Kiểm tra sự tồn tại của SKU sản phẩm.
    /// - VALIDATION 3: Kiểm tra trạng thái hết hàng (InStock = false hoặc Stock <= 0).
    /// - RÀNG BUỘC TỒN KHO: Tổng số lượng trong giỏ không được vượt quá số tồn kho hiện tại.
    /// </summary>
    /// -----------------------------------------------------------------------------------------------------------------------------
    //Phương thức AddItemAsync() - Thêm sản phẩm & Check điều kiện: ( VALIDATE ) 
    public async Task<(bool Success, string Message)> AddItemAsync(string sku, int quantity)
    {
        if (quantity < 1)  //Check 1: Số lượng thêm phải >= 1.
        {
            return (false, "Số lượng không hợp lệ.");
        }
        // Tìm mã SKU trong CSDL, nếu không tồn tại ➔ Báo lỗi "Không tìm thấy sản phẩm".
        var product = await _productResolver.ResolveAsync(sku);
        if (product == null)
        {
            return (false, "Không tìm thấy sản phẩm.");
        }
        //Check 3: Kiểm tra cờ InStock == false hoặc Stock <= 0 ➔ Báo lỗi sản phẩm đã hết hàng.
        if (!product.InStock || product.Stock <= 0)
        {
            return (false, "sản phẩm đã hết hàng , vui lòng chọn sản phẩm khác");
        }

        //(Xử lý sản phẩm đã có sẵn trong giỏ)
        var items = GetCartItems();
        var existing = items.FirstOrDefault(i => i.Sku.Equals(product.Sku, StringComparison.OrdinalIgnoreCase));

        if (existing != null)
        {
            //Nếu sản phẩm đã nằm trong giỏ trước đó ➔ Cộng dồn số lượng existing.Quantity + quantity.

            //iếp tục dùng Math.Min(..., product.Stock) để chặn không cho tổng số lượng vượt quá kho. 
            //Nếu đã đạt tối đa tồn kho ➔ Trả về message thông báo "Chỉ còn X sản phẩm trong kho".
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

        SaveCartItems(items);   //Lưu danh sách giỏ hàng sau khi cập nhật vào HttpContext.Session dưới dạng chuỗi JSON mã hóa.
        return (true, "Đã thêm sản phẩm vào giỏ hàng.");
    }

    /// <summary>
    /// LUỒNG MANAGE CART: Cập nhật số lượng trực tiếp cho 1 sản phẩm
    /// - Nếu quantity < 1 -> Gọi xóa sản phẩm khỏi giỏ.
    /// - Giới hạn số lượng bởi Stock hiện tại.
    /// </summary>
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

    //
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

    /// <summary>
    /// LUỒNG MANAGE CART: Áp dụng mã giảm giá (Voucher)
    /// - VALIDATION: Kiểm tra giỏ hàng có trống không, mã voucher có hợp lệ và còn hạn dùng không, kiểm tra giá trị đơn tối thiểu (MinOrder).
    /// </summary>
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

    /// <summary>
    /// RÀNG BUỘC VOUCHER: Tính toán tiền giảm theo phần trăm (%) hoặc số tiền cố định
    /// - Kiểm tra trạng thái Voucher (Status == true) và ngày hết hạn (ExpiryDate >= Today).
    /// - Kiểm tra đơn hàng có đạt giá trị tối thiểu (subtotal >= MinOrder).
    /// </summary>
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
