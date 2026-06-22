/**
 * Project: Pet Store Management System (PSMS)
 * File: ProductCategoryController.cs
 * Author: Tran Duong
 * Date: May 31, 2026
 * Description: Xử lý các yêu cầu HTTP cho chức năng quản lý danh mục sản phẩm trong khu vực Warehouse (thêm, sửa, xóa danh mục).
 */
using ManagePetStore.Exceptions;
using ManagePetStore.Models;
using ManagePetStore.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ManagePetStore.Controllers
{
    [Authorize(Roles = "warehouse,admin")]
    [Route("Warehouse/ProductCategory/{action=Index}/{id?}")]
public class WarehouseProductCategoryController : Controller
    {
        private readonly IProductCategoryService _categoryService;

        public WarehouseProductCategoryController(IProductCategoryService categoryService)
        {
            _categoryService = categoryService;
        }

        // Hiển thị danh sách danh mục sản phẩm
        public async Task<IActionResult> Index(bool showDeleted = false)
        {
            var summary = await _categoryService.GetCategorySummary(showDeleted);

            ViewBag.TotalCategories = summary.TotalCategories;
            ViewBag.TotalProducts   = summary.TotalProducts;
            ViewBag.EmptyCategories = summary.EmptyCategories;
            ViewBag.ShowDeleted     = showDeleted;

            return View(summary.Categories);
        }

        // Hiển thị form thêm mới danh mục sản phẩm
        public IActionResult Create()
        {
            return View();
        }

        // Xử lý thêm mới danh mục sản phẩm
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            [Bind("CategoryId,Name,Description")] ProductCategory category)
        {
            if (!ModelState.IsValid) return View(category);

            try
            {
                await _categoryService.CreateCategory(category);
                return RedirectToAction(nameof(Index));
            }
            catch (ServiceException ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
                return View(category);
            }
        }

        // Hiển thị form chỉnh sửa danh mục sản phẩm
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var category = await _categoryService.GetCategoryById(id.Value);
            if (category == null) return NotFound();

            return View(category);
        }

        // Xử lý cập nhật danh mục sản phẩm
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            int id,
            [Bind("CategoryId,Name,Description")] ProductCategory category)
        {
            if (!ModelState.IsValid) return View(category);

            try
            {
                await _categoryService.UpdateCategory(id, category);
                return RedirectToAction(nameof(Index));
            }
            catch (ServiceException ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
                return View(category);
            }
        }

        // Xử lý xóa (hoặc ẩn) danh mục sản phẩm
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            await _categoryService.DeleteCategory(id);
            return RedirectToAction(nameof(Index));
        }

        // Xử lý khôi phục danh mục sản phẩm đã xóa
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Restore(int id)
        {
            await _categoryService.RestoreCategory(id);
            return RedirectToAction(nameof(Index), new { showDeleted = true });
        }
    }
}






