/**
 * Project: Pet Store Management System (PSMS)
 * File: SearchController.cs
 * Author: Tran Duong
 * Last Update: July 17, 2026
 * Description: API endpoint tráº£ vá» gá»£i Ã½ tÃ¬m kiáº¿m nhanh cho Warehouse sidebar search.
 */
using ManagePetStore.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ManagePetStore.Areas.Warehouse.Controllers;

[Area("Warehouse")]
[Authorize(Roles = "warehouse,admin")]
public class SearchController : Controller
{
    private readonly IProductService _productService;
    private readonly ISupplierService _supplierService;

    public SearchController(IProductService productService, ISupplierService supplierService)
    {
        _productService = productService;
        _supplierService = supplierService;
    }

    /// <summary>
    /// Tráº£ vá» gá»£i Ã½ sáº£n pháº©m theo tá»« khÃ³a (dÃ¹ng cho AJAX autocomplete)
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> ProductSuggestions(string q)
    {
        if (string.IsNullOrWhiteSpace(q) || q.Length < 1)
            return Json(new List<object>());

        var summary = await _productService.GetProductSummary(q, "active");
        var results = summary.Products
            .Take(7)
            .Select(p => new
            {
                label = p.Name,
                sub   = p.Sku,
                url   = Url.Action("Index", "Product", new { area = "Warehouse", search = q })
            });

        return Json(results);
    }

    /// <summary>
    /// Tráº£ vá» gá»£i Ã½ nhÃ  cung cáº¥p theo tá»« khÃ³a (dÃ¹ng cho AJAX autocomplete)
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> SupplierSuggestions(string q)
    {
        if (string.IsNullOrWhiteSpace(q) || q.Length < 1)
            return Json(new List<object>());

        var all = await _supplierService.GetAllSuppliersAsync();
        var results = all
            .Where(s => s.Name.Contains(q, StringComparison.OrdinalIgnoreCase)
                     || (s.Phone != null && s.Phone.Contains(q))
                     || (s.Email != null && s.Email.Contains(q, StringComparison.OrdinalIgnoreCase)))
            .Take(7)
            .Select(s => new
            {
                label = s.Name,
                sub   = s.Phone ?? s.Email ?? "",
                url   = Url.Action("Index", "Supplier", new { area = "Warehouse" })
            });

        return Json(results);
    }
}
