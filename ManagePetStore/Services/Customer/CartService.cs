
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
        var items = GetCartItems();  // // Đọc danh sách sản phẩm từ Session ("ShoppingCart")
        var viewModel = new CartPageViewModel();

        foreach (var item in items)
        {
            //Đọc thông tin sản phẩm từ CSDL qua _productResolver.
            var product = await _productResolver.ResolveAsync(item.Sku);  //  Đọc thông tin sản phẩm mới nhất từ CSDL qua mã SKU
            if (product == null)
            {
                continue;  //// Sản phẩm đã bị xóa khỏi CSDL -> Bỏ qua
            }


            // RÀNG BUỘC TỒN KHO: Số lượng trong giỏ không được vượt quá số tồn thực tế trong kho
            // // Nếu số lượng lưu trong Session (item.Quantity) lớn hơn Tồn kho trong CSDL (product.Stock)
            // ➔ Tự động ép về đúng số lượng tồn kho khả dụng (Math.Min)
            var quantity = Math.Min(item.Quantity, product.Stock);
            if (quantity <= 0)
            {
                continue;  //// Nếu hết hàng (Stock = 0) -> Bỏ qua
            }

            viewModel.Items.Add(new CartLineItemViewModel
            {
                Sku = product.Sku,
                Name = product.Name,
                ImageUrl = product.ImageUrl,
                UnitPrice = product.Price,  // // Lấy giá niêm yết mới nhất từ CSDL
                Quantity = quantity,
                MaxStock = product.Stock  //// Giới hạn nút Tăng số lượng ở FE
            });
        }
        //Cập nhật lại Session giỏ hàng sau khi đã chuẩn hóa với tồn kho CSDL
        SaveCartItems(viewModel.Items.Select(i => new CartSessionItem
        {
            Sku = i.Sku,
            Name = i.Name,
            Price = i.UnitPrice,
            ImageUrl = i.ImageUrl,
            Quantity = i.Quantity,
            MaxStock = i.MaxStock
        }).ToList());

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

            existing.Quantity = newQty;   /// Cập nhật số lượng mới
            existing.Price = product.Price;
            existing.Name = product.Name;
            existing.ImageUrl = product.ImageUrl;
            existing.MaxStock = product.Stock;
        }
        else
        {
            //        // Thêm mới sản phẩm vào giỏ
            items.Add(new CartSessionItem
            {
                Sku = product.Sku,
                Name = product.Name,
                Price = product.Price,
                ImageUrl = product.ImageUrl,
                Quantity = Math.Min(quantity, product.Stock),  // kiểm soát trần tồn kho 
                MaxStock = product.Stock
            });
        }

        SaveCartItems(items);   /// Lưu lại chuỗi JSON mã hóa vào HttpContext.Session
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

    // hàm tăng số lượng sản phẩm trong giỏ hàng 
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


    //    // hàm giảm số lượng sản phẩm trong giỏ hàng
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

    // hàm xóa sản phẩm trong giỏ hàng 
    public Task<(bool Success, string Message)> RemoveItemAsync(string sku)
    {
        var items = GetCartItems();
        items.RemoveAll(i => i.Sku.Equals(sku, StringComparison.OrdinalIgnoreCase));
        SaveCartItems(items);
        return Task.FromResult((true, "Đã xóa sản phẩm khỏi giỏ hàng."));
    }

    public void ClearCart()
    {
        _httpContextAccessor.HttpContext?.Session.Remove(CartSessionKey);
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
