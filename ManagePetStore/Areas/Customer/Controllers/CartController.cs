using ManagePetStore.Areas.Customer.Services;
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

    [HttpGet]
    public async Task<IActionResult> Index(string? searchTerm, int page = 1)
    {
        var model = await _cartService.GetCartPageAsync();
        var normalizedSearch = searchTerm?.Trim() ?? "";

        var filteredItems = model.Items
            .Where(i => string.IsNullOrWhiteSpace(normalizedSearch) ||
                        i.Name.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase) ||
                        i.Sku.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase))
            .ToList();

        var currentPage = page < 1 ? 1 : page;
        var totalFilteredItems = filteredItems.Count;
        var totalPages = totalFilteredItems == 0
            ? 0
            : (int)Math.Ceiling(totalFilteredItems / (double)model.PageSize);

        if (totalPages > 0 && currentPage > totalPages)
        {
            currentPage = totalPages;
        }

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

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Add(string sku, int quantity = 1, string? returnUrl = null)
    {
        var (success, message) = await _cartService.AddItemAsync(sku, quantity);

        if (success)
        {
            TempData["SuccessMessage"] = message;
            return RedirectToAction(nameof(Index));
        }

        TempData["ErrorMessage"] = message;
        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Increase(string sku, string? searchTerm, int page = 1)
    {
        var (success, message) = await _cartService.IncreaseQuantityAsync(sku);
        TempData[success ? "SuccessMessage" : "ErrorMessage"] = message;
        return RedirectToAction(nameof(Index), new { searchTerm, page });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Decrease(string sku, string? searchTerm, int page = 1)
    {
        var (success, message) = await _cartService.DecreaseQuantityAsync(sku);
        TempData[success ? "SuccessMessage" : "ErrorMessage"] = message;
        return RedirectToAction(nameof(Index), new { searchTerm, page });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Remove(string sku, string? searchTerm, int page = 1)
    {
        var (_, message) = await _cartService.RemoveItemAsync(sku);
        TempData["SuccessMessage"] = message;
        return RedirectToAction(nameof(Index), new { searchTerm, page });
    }
}
