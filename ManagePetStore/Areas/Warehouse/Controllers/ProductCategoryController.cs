/**
 * Project: Pet Store Management System (PSMS)
 * File: ProductCategoryController.cs
 * Author: Tran Duong
 * Date: May 31, 2026
 * Last Update: July 17, 2026
 * Description: Xá»­ lÃ½ cÃ¡c yÃªu cáº§u HTTP cho chá»©c nÄƒng quáº£n lÃ½ danh má»¥c sáº£n pháº©m trong khu vá»±c Warehouse (thÃªm, sá»­a, xÃ³a danh má»¥c).
 */
using ManagePetStore.Exceptions;
using ManagePetStore.Models;
using ManagePetStore.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ManagePetStore.Areas.Warehouse.Controllers
{
    [Area("Warehouse")]
    [Authorize(Roles = "warehouse,admin")]
    public class ProductCategoryController : Controller
    {
        private readonly IProductCategoryService _categoryService;

        public ProductCategoryController(IProductCategoryService categoryService)
        {
            _categoryService = categoryService;
        }

        // Hiá»ƒn thá»‹ danh sÃ¡ch danh má»¥c sáº£n pháº©m
        public async Task<IActionResult> Index(bool showDeleted = false)
        {
            var summary = await _categoryService.GetCategorySummary(showDeleted);

            ViewBag.TotalCategories = summary.TotalCategories;
            ViewBag.TotalProducts   = summary.TotalProducts;
            ViewBag.EmptyCategories = summary.EmptyCategories;
            ViewBag.ShowDeleted     = showDeleted;

            return View(summary.Categories);
        }

        // Hiá»ƒn thá»‹ form thÃªm má»›i danh má»¥c sáº£n pháº©m
        public IActionResult Create()
        {
            return View();
        }

        // Xá»­ lÃ½ thÃªm má»›i danh má»¥c sáº£n pháº©m
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            [Bind("CategoryId,Name,Description,Keywords")] ProductCategory category)
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

        // Hiá»ƒn thá»‹ form chá»‰nh sá»­a danh má»¥c sáº£n pháº©m
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var category = await _categoryService.GetCategoryById(id.Value);
            if (category == null) return NotFound();

            return View(category);
        }

        // Xá»­ lÃ½ cáº­p nháº­t danh má»¥c sáº£n pháº©m
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            int id,
            [Bind("CategoryId,Name,Description,Keywords")] ProductCategory category)
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

        // Xá»­ lÃ½ xÃ³a (hoáº·c áº©n) danh má»¥c sáº£n pháº©m
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            await _categoryService.DeleteCategory(id);
            return RedirectToAction(nameof(Index));
        }

        // Xá»­ lÃ½ khÃ´i phá»¥c danh má»¥c sáº£n pháº©m Ä‘Ã£ xÃ³a
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Restore(int id)
        {
            await _categoryService.RestoreCategory(id);
            return RedirectToAction(nameof(Index), new { showDeleted = true });
        }
    }
}
