/**
 * Project: Pet Store Management System (PSMS)
 * File: SearchController.cs
 * Author: Tran Duong
 * Description: API endpoint trả về gợi ý tìm kiếm nhanh cho Warehouse sidebar search.
 */
using ManagePetStore.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ManagePetStore.Controllers;

[Authorize(Roles = "warehouse,admin")]
[Route("Warehouse/Search/{action=Index}/{id?}")]
public class WarehouseSearchController : Controller
{
    private readonly IProductService _productService;
    private readonly ISupplierService _supplierService;

    public WarehouseSearchController(IProductService productService, ISupplierService supplierService)
    {
        _productService = productService;
        _supplierService = supplierService;
    }

    /// <summary>
    /// Trả về gợi ý sản phẩm theo từ khóa (dùng cho AJAX autocomplete)
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
    /// Trả về gợi ý nhà cung cấp theo từ khóa (dùng cho AJAX autocomplete)
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




