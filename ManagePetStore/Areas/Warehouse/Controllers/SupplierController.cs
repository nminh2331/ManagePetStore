using ManagePetStore.Models;
using ManagePetStore.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ManagePetStore.Areas.Warehouse.Controllers;

[Area("Warehouse")]
public class SupplierController : Controller
{
    private readonly ISupplierService _supplierService;
    private readonly IProductCategoryService _categoryService;

    public SupplierController(ISupplierService supplierService, IProductCategoryService categoryService)
    {
        _supplierService = supplierService;
        _categoryService = categoryService;
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
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Supplier supplier, List<int> categoryIds)
    {
        if (ModelState.IsValid)
        {
            await _supplierService.AddSupplierAsync(supplier, categoryIds);
            TempData["SuccessMessage"] = "Thêm nhà cung cấp thành công!";
            return RedirectToAction(nameof(Index));
        }
        var categories = (await _categoryService.GetCategorySummary()).Categories;
        ViewBag.Categories = new MultiSelectList(categories, "CategoryId", "Name", categoryIds);
        return View(supplier);
    }

    public async Task<IActionResult> Edit(int id)
    {
        var supplier = await _supplierService.GetSupplierByIdAsync(id);
        if (supplier == null) return NotFound();

        var categories = (await _categoryService.GetCategorySummary()).Categories;
        var selectedCategoryIds = supplier.SupplierCategories.Select(sc => sc.CategoryId).ToList();
        ViewBag.Categories = new MultiSelectList(categories, "CategoryId", "Name", selectedCategoryIds);
        
        return View(supplier);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Supplier supplier, List<int> categoryIds)
    {
        if (id != supplier.SupplierId) return NotFound();

        if (ModelState.IsValid)
        {
            await _supplierService.UpdateSupplierAsync(supplier, categoryIds);
            TempData["SuccessMessage"] = "Cập nhật nhà cung cấp thành công!";
            return RedirectToAction(nameof(Index));
        }
        var categories = (await _categoryService.GetCategorySummary()).Categories;
        ViewBag.Categories = new MultiSelectList(categories, "CategoryId", "Name", categoryIds);
        return View(supplier);
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int id)
    {
        await _supplierService.DeleteSupplierAsync(id);
        TempData["SuccessMessage"] = "Xóa nhà cung cấp thành công!";
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
