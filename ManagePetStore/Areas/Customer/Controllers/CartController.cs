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
    public async Task<IActionResult> Index()
    {
        var model = await _cartService.GetCartPageAsync();
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
    public async Task<IActionResult> Increase(string sku)
    {
        var (success, message) = await _cartService.IncreaseQuantityAsync(sku);
        TempData[success ? "SuccessMessage" : "ErrorMessage"] = message;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Decrease(string sku)
    {
        var (success, message) = await _cartService.DecreaseQuantityAsync(sku);
        TempData[success ? "SuccessMessage" : "ErrorMessage"] = message;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Remove(string sku)
    {
        var (_, message) = await _cartService.RemoveItemAsync(sku);
        TempData["SuccessMessage"] = message;
        return RedirectToAction(nameof(Index));
    }
}
