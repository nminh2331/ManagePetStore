/**
 * Project: Pet Store Management System (PSMS)
 * File: ProductController.cs
 * Author: Tran Duong
 * Date: May 31, 2026
 * Description: Xử lý các yêu cầu HTTP cho chức năng quản lý sản phẩm trong kho hàng (thêm, sửa, xóa sản phẩm và theo dõi tình trạng tồn kho).
 */
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
            var summary = await _productService.GetProductSummary();

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
            ViewData["CategoryId"] = await _productService.GetCategorySelectList();
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
                ViewData["CategoryId"] = await _productService.GetCategorySelectList(product.CategoryId);
                return View(product);
            }

            try
            {
                await _productService.CreateProduct(product);
                return RedirectToAction(nameof(Index));
            }
            catch (ServiceException ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
                ViewData["CategoryId"] = await _productService.GetCategorySelectList(product.CategoryId);
                return View(product);
            }
        }

        // GET: Warehouse/Product/Edit/{sku}
        public async Task<IActionResult> Edit(string id)
        {
            if (id == null) return NotFound();

            var product = await _productService.GetProductBySku(id);
            if (product == null) return NotFound();

            ViewData["CategoryId"] = await _productService.GetCategorySelectList(product.CategoryId);
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
                ViewData["CategoryId"] = await _productService.GetCategorySelectList(product.CategoryId);
                return View(product);
            }

            try
            {
                await _productService.UpdateProduct(id, product);
                return RedirectToAction(nameof(Index));
            }
            catch (ServiceException ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
                ViewData["CategoryId"] = await _productService.GetCategorySelectList(product.CategoryId);
                return View(product);
            }
        }

        // POST: Warehouse/Product/Delete/{sku}
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(string id)
        {
            await _productService.DeleteProduct(id);
            return RedirectToAction(nameof(Index));
        }
    }
}
