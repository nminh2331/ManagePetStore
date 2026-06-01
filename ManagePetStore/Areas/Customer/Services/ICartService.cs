using ManagePetStore.Areas.Customer.Models;

namespace ManagePetStore.Areas.Customer.Services;

public interface ICartService
{
    Task<CartPageViewModel> GetCartPageAsync();
    int GetTotalQuantity();
    Task<(bool Success, string Message)> AddItemAsync(string sku, int quantity);
    Task<(bool Success, string Message)> SetQuantityAsync(string sku, int quantity);
    Task<(bool Success, string Message)> IncreaseQuantityAsync(string sku);
    Task<(bool Success, string Message)> DecreaseQuantityAsync(string sku);
    Task<(bool Success, string Message)> RemoveItemAsync(string sku);
    Task<(bool Success, string Message)> ApplyVoucherAsync(string code);
    void ClearVoucher();
    void ClearCart();
}
