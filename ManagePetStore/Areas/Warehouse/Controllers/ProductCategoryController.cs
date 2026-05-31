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

        // GET: Warehouse/ProductCategory
        public async Task<IActionResult> Index()
        {
            var summary = await _categoryService.GetSummaryAsync();

            ViewBag.TotalCategories = summary.TotalCategories;
            ViewBag.TotalProducts   = summary.TotalProducts;
            ViewBag.EmptyCategories = summary.EmptyCategories;

            return View(summary.Categories);
        }

        // GET: Warehouse/ProductCategory/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Warehouse/ProductCategory/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            [Bind("CategoryId,Name,Description")] ProductCategory category)
        {
            if (!ModelState.IsValid) return View(category);

            try
            {
                await _categoryService.CreateAsync(category);
                return RedirectToAction(nameof(Index));
            }
            catch (ServiceException ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
                return View(category);
            }
        }

        // GET: Warehouse/ProductCategory/Edit/{id}
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var category = await _categoryService.GetByIdAsync(id.Value);
            if (category == null) return NotFound();

            return View(category);
        }

        // POST: Warehouse/ProductCategory/Edit/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            int id,
            [Bind("CategoryId,Name,Description")] ProductCategory category)
        {
            if (!ModelState.IsValid) return View(category);

            try
            {
                await _categoryService.UpdateAsync(id, category);
                return RedirectToAction(nameof(Index));
            }
            catch (ServiceException ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
                return View(category);
            }
        }

        // POST: Warehouse/ProductCategory/Delete/{id}
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            await _categoryService.DeleteAsync(id);
            return RedirectToAction(nameof(Index));
        }
    }
}
