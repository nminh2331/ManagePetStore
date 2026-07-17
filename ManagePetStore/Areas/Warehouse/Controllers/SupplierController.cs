/**
 * Project: Pet Store Management System (PSMS)
 * File: SupplierController.cs
 * Author: Tran Duong
 * Date: June 17, 2026
 * Last Update: July 17, 2026
 * Description: Controller xá»­ lÃ½ nghiá»‡p vá»¥ quáº£n lÃ½ nhÃ  cung cáº¥p.
 */
using ManagePetStore.Models;
using ManagePetStore.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ManagePetStore.Areas.Warehouse.Controllers;

[Area("Warehouse")]
[Authorize(Roles = "warehouse,admin")]
public class SupplierController : Controller
{
    private readonly ISupplierService _supplierService;
    private readonly IProductCategoryService _categoryService;
    private readonly IProductService _productService;

    public SupplierController(ISupplierService supplierService, IProductCategoryService categoryService, IProductService productService)
    {
        _supplierService = supplierService;
        _categoryService = categoryService;
        _productService  = productService;
    }

    public async Task<IActionResult> Index()
    {
        var suppliers = await _supplierService.GetAllSuppliersAsync();
        return View(suppliers);
    }

    public async Task<IActionResult> Create()
    {
        var categories = (await _categoryService.GetCategorySummary()).Categories;
        ViewBag.Categories = new MultiSelectList(categories, "CategoryId", "Name");
        var allProducts = await _productService.GetProductSummary("", "active");
        ViewBag.AllProducts = allProducts.Products;
        ViewBag.SelectedProductSkus = new List<string>();
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Supplier supplier, List<int> categoryIds, List<string>? productSkus)
    {
        if (ModelState.IsValid)
        {
            await _supplierService.AddSupplierAsync(supplier, categoryIds);
            if (productSkus != null && productSkus.Any())
                await _supplierService.UpdateSupplierProductsAsync(supplier.SupplierId, productSkus);
            TempData["SuccessMessage"] = "ThÃªm nhÃ  cung cáº¥p thÃ nh cÃ´ng!";
            return RedirectToAction(nameof(Index));
        }
        var categories = (await _categoryService.GetCategorySummary()).Categories;
        ViewBag.Categories = new MultiSelectList(categories, "CategoryId", "Name", categoryIds);
        var allProducts = await _productService.GetProductSummary("", "active");
        ViewBag.AllProducts = allProducts.Products;
        ViewBag.SelectedProductSkus = productSkus ?? new List<string>();
        return View(supplier);
    }

    public async Task<IActionResult> Edit(int id)
    {
        var supplier = await _supplierService.GetSupplierByIdAsync(id);
        if (supplier == null) return NotFound();

        var categories = (await _categoryService.GetCategorySummary()).Categories;
        var selectedCategoryIds = supplier.Categories.Select(c => c.CategoryId).ToList();
        ViewBag.Categories = new MultiSelectList(categories, "CategoryId", "Name", selectedCategoryIds);

        var allProducts = await _productService.GetProductSummary("", "active");
        ViewBag.AllProducts = allProducts.Products;
        ViewBag.SelectedProductSkus = await _supplierService.GetSupplierProductSkusAsync(id);

        return View(supplier);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Supplier supplier, List<int> categoryIds, List<string>? productSkus)
    {
        if (id != supplier.SupplierId) return NotFound();

        if (ModelState.IsValid)
        {
            await _supplierService.UpdateSupplierAsync(supplier, categoryIds);
            await _supplierService.UpdateSupplierProductsAsync(supplier.SupplierId, productSkus ?? new List<string>());
            TempData["SuccessMessage"] = "Cáº­p nháº­t nhÃ  cung cáº¥p thÃ nh cÃ´ng!";
            return RedirectToAction(nameof(Index));
        }
        var categories = (await _categoryService.GetCategorySummary()).Categories;
        ViewBag.Categories = new MultiSelectList(categories, "CategoryId", "Name", categoryIds);
        var allProducts = await _productService.GetProductSummary("", "active");
        ViewBag.AllProducts = allProducts.Products;
        ViewBag.SelectedProductSkus = productSkus ?? new List<string>();
        return View(supplier);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Deactivate(int id)
    {
        await _supplierService.DeleteSupplierAsync(id);
        TempData["SuccessMessage"] = "ÄÃ£ Ä‘Ã¡nh dáº¥u nhÃ  cung cáº¥p ngá»«ng hoáº¡t Ä‘á»™ng.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Activate(int id)
    {
        var supplier = await _supplierService.GetSupplierByIdAsync(id);
        if (supplier != null)
        {
            supplier.IsActive = true;
            // Since UpdateSupplierAsync expects categoryIds, we can pass existing ones
            var categoryIds = supplier.Categories.Select(c => c.CategoryId).ToList();
            await _supplierService.UpdateSupplierAsync(supplier, categoryIds);
            TempData["SuccessMessage"] = "ÄÃ£ kÃ­ch hoáº¡t láº¡i nhÃ  cung cáº¥p.";
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> GetSuppliersByCategory(int categoryId)
    {
        var suppliers = await _supplierService.GetSuppliersByCategoryAsync(categoryId);
        var result = suppliers.Select(s => new {
            s.SupplierId,
            s.Name
        });
        return Json(result);
    }
}
