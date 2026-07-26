// HÀ HOÀNG HIỆP CODE - LUỒNG MUA HÀNG: USE CASE MANAGE CART (QUẢN LÝ GIỎ HÀNG)
using ManagePetStore.Services.Customer;
using Microsoft.AspNetCore.Mvc;

namespace ManagePetStore.Areas.Customer.Controllers;

[Area("Customer")]
public class CartController : Controller
{
    private readonly ICartService _cartService;

    public CartController(ICartService cartService)
    {
        _cartService = cartService;
    }

    /// <summary>
    /// LUỒNG MANAGE CART: Trang danh sách giỏ hàng
    /// - Gọi ICartService lấy dữ liệu giỏ hàng chuẩn hóa (sản phẩm, giá, số lượng tồn kho, voucher).
    /// - Xử lý tìm kiếm theo Tên sản phẩm / Mã SKU (không phân biệt hoa thường).
    /// - Kiểm tra và tính toán phân trang (Pagination), chống tình trạng số trang vượt quá tổng số trang hợp lệ.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Index(string? searchTerm, int page = 1)
    {
        // 1. Lấy dữ liệu giỏ hàng đã được chuẩn hóa từ Session/Database
        var model = await _cartService.GetCartPageAsync();

        // 2. Validate & Chuẩn hóa từ khóa tìm kiếm
        var normalizedSearch = searchTerm?.Trim() ?? "";

        // 3. Lọc danh sách sản phẩm theo từ khóa (Tìm theo Tên hoặc SKU)
        var filteredItems = model.Items   
            .Where(i => string.IsNullOrWhiteSpace(normalizedSearch) ||
                        i.Name.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase) ||
                        i.Sku.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase))
            .ToList();

        // 4. RÀNG BUỘC PHÂN TRANG: Kiểm tra trang hiện tại và tính tổng số trang
        var currentPage = page < 1 ? 1 : page;
        var totalFilteredItems = filteredItems.Count;
        var totalPages = totalFilteredItems == 0
            ? 0
            : (int)Math.Ceiling(totalFilteredItems / (double)model.PageSize);

        // Chống lỗi truy cập trang ngoài phạm vi (Nếu page > totalPages thì đưa về trang cuối)
        if (totalPages > 0 && currentPage > totalPages)
        {
            currentPage = totalPages;
        }

        // 5. Gán dữ liệu hiển thị cho ViewModel
        model.SearchTerm = normalizedSearch;
        model.Page = currentPage;
        model.TotalFilteredItems = totalFilteredItems;
        model.TotalPages = totalPages;
        model.FilteredQuantity = filteredItems.Sum(i => i.Quantity);
        model.VisibleItems = filteredItems
            .Skip((currentPage - 1) * model.PageSize)
            .Take(model.PageSize)
            .ToList();

        return View(model);
    }

    /// <summary>
    /// LUỒNG MANAGE CART: Thêm sản phẩm vào giỏ hàng
    /// - VALIDATION: Kiểm tra SKU, số lượng thêm phải >= 1, kiểm tra số lượng tồn kho khả dụng.
    /// - Trả về thông báo thành công hoặc lỗi qua TempData và điều hướng người dùng.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Add(string sku, int quantity = 1, string? returnUrl = null)
    {
        // Gọi Service thực hiện thêm sản phẩm & kiểm tra tồn kho kho hàng
        var (success, message) = await _cartService.AddItemAsync(sku, quantity);

        if (success)
        {
            TempData["SuccessMessage"] = message;
            return RedirectToAction(nameof(Index));
        }

        TempData["ErrorMessage"] = message;
        // Trở về trang trước đó nếu URL hợp lệ (Local URL)
        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// LUỒNG MANAGE CART: Tăng số lượng 1 sản phẩm trong giỏ
    /// - RÀNG BUỘC: Kiểm tra số lượng tồn kho hiện tại của sản phẩm trước khi cho phép tăng.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Increase(string sku, string? searchTerm, int page = 1)
    {
        var (success, message) = await _cartService.IncreaseQuantityAsync(sku);
        TempData[success ? "SuccessMessage" : "ErrorMessage"] = message;
        return RedirectToAction(nameof(Index), new { searchTerm, page });
    }

    /// <summary>
    /// LUỒNG MANAGE CART: Giảm số lượng 1 sản phẩm trong giỏ
    /// - RÀNG BUỘC: Nếu số lượng giảm về 0, tự động xóa sản phẩm đó khỏi giỏ hàng.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Decrease(string sku, string? searchTerm, int page = 1)
    {
        var (success, message) = await _cartService.DecreaseQuantityAsync(sku);
        TempData[success ? "SuccessMessage" : "ErrorMessage"] = message;
        return RedirectToAction(nameof(Index), new { searchTerm, page });
    }

    /// <summary>
    /// LUỒNG MANAGE CART: Xóa sản phẩm khỏi giỏ hàng
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Remove(string sku, string? searchTerm, int page = 1)
    {
        var (_, message) = await _cartService.RemoveItemAsync(sku);
        TempData["SuccessMessage"] = message;
        return RedirectToAction(nameof(Index), new { searchTerm, page });
    }
}
