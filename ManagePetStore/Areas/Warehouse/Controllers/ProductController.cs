using ManagePetStore.Exceptions;
using ManagePetStore.Models;
using ManagePetStore.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ManagePetStore.Areas.Warehouse.Controllers
{
    [Area("Warehouse")]
    [Authorize(Roles = "warehouse,admin")]
    public class ProductController : Controller
    {
        private readonly IProductService _productService;

        public ProductController(IProductService productService)
        {
            _productService = productService;
        }

        // GET: Warehouse/Product
        public async Task<IActionResult> Index()
        {
            var summary = await _productService.GetSummaryAsync();

            ViewBag.TotalProducts   = summary.TotalProducts;
            ViewBag.LowStockCount   = summary.LowStockCount;
            ViewBag.OutOfStockCount = summary.OutOfStockCount;
            ViewBag.TotalValue      = summary.TotalValue;
            ViewBag.CategoryCount   = summary.CategoryCount;

            return View(summary.Products);
        }

        // GET: Warehouse/Product/Create
        public async Task<IActionResult> Create()
        {
            ViewData["CategoryId"] = await _productService.GetCategorySelectListAsync();
            return View();
        }

        // POST: Warehouse/Product/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            [Bind("Sku,Name,CategoryId,Unit,Stock,MinStock,ExpiryDate,ShelfLocation,Price,ImageUrl")]
            Product product)
        {
            if (!ModelState.IsValid)
            {
                ViewData["CategoryId"] = await _productService.GetCategorySelectListAsync(product.CategoryId);
                return View(product);
            }

            try
            {
                await _productService.CreateAsync(product);
                return RedirectToAction(nameof(Index));
            }
            catch (ServiceException ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
                ViewData["CategoryId"] = await _productService.GetCategorySelectListAsync(product.CategoryId);
                return View(product);
            }
        }

        // GET: Warehouse/Product/Edit/{sku}
        public async Task<IActionResult> Edit(string id)
        {
            if (id == null) return NotFound();

            var product = await _productService.GetBySkuAsync(id);
            if (product == null) return NotFound();

            ViewData["CategoryId"] = await _productService.GetCategorySelectListAsync(product.CategoryId);
            return View(product);
        }

        // POST: Warehouse/Product/Edit/{sku}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            string id,
            [Bind("Sku,Name,CategoryId,Unit,Stock,MinStock,ExpiryDate,ShelfLocation,Price,ImageUrl")]
            Product product)
        {
            if (!ModelState.IsValid)
            {
                ViewData["CategoryId"] = await _productService.GetCategorySelectListAsync(product.CategoryId);
                return View(product);
            }

            try
            {
                await _productService.UpdateAsync(id, product);
                return RedirectToAction(nameof(Index));
            }
            catch (ServiceException ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
                ViewData["CategoryId"] = await _productService.GetCategorySelectListAsync(product.CategoryId);
                return View(product);
            }
        }

        // POST: Warehouse/Product/Delete/{sku}
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(string id)
        {
            await _productService.DeleteAsync(id);
            return RedirectToAction(nameof(Index));
        }
    }
}
