
// HÀ HOÀNG HIỆP CODE


using ManagePetStore.Areas.Customer.Models;

namespace ManagePetStore.Services.Customer;

public interface ICartService
{
    Task<CartPageViewModel> GetCartPageAsync();
    int GetTotalQuantity();
    Task<(bool Success, string Message)> AddItemAsync(string sku, int quantity);  //thêm item vào cart.
    Task<(bool Success, string Message)> SetQuantityAsync(string sku, int quantity);
    Task<(bool Success, string Message)> IncreaseQuantityAsync(string sku);  //tăng 1.
    Task<(bool Success, string Message)> DecreaseQuantityAsync(string sku);  // giảm 1 
    Task<(bool Success, string Message)> RemoveItemAsync(string sku);  // xóa item.
    Task<(bool Success, string Message)> ApplyVoucherAsync(string code);  // add mã giảm giá
    void ClearVoucher();
    void ClearCart();  //xóa sạch giỏ hàng.
}
