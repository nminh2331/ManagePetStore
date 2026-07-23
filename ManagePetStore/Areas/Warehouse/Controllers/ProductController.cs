/**
 * Project: Pet Store Management System (PSMS)
 * File: ProductController.cs
 * Author: Tran Duong
 * Date: May 31, 2026
 * Last Update: July 23, 2026
 * Description: Xử lý các yêu cầu HTTP cho chức năng quản lý sản phẩm trong kho hàng (thêm, sửa, xóa sản phẩm và theo dõi tình trạng tồn kho).
 */
using ManagePetStore.Exceptions;
using ManagePetStore.Models;
using ManagePetStore.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ManagePetStore.Services.Warehouse;
using ManagePetStore.Repositories.Warehouse;
using System;
using System.IO;
using System.Linq;

namespace ManagePetStore.Areas.Warehouse.Controllers
{
    [Area("Warehouse")]
    [Authorize(Roles = "warehouse,admin")]
    public class ProductController : Controller
    {
        private readonly IProductService _productService;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly IInventoryBatchService _batchService;
        private readonly IStockMovementRepository _movementRepo;

        public ProductController(
            IProductService productService,
            IWebHostEnvironment webHostEnvironment,
            IInventoryBatchService batchService,
            IStockMovementRepository movementRepo)
        {
            _productService = productService;
            _webHostEnvironment = webHostEnvironment;
            _batchService = batchService;
            _movementRepo = movementRepo;
        }

        // Hiển thị danh sách sản phẩm
        public async Task<IActionResult> Index(string search, string filter = "active")
        {
            var summary = await _productService.GetProductSummary(search, filter);

            ViewBag.TotalProducts   = summary.TotalProducts;
            ViewBag.LowStockCount   = summary.LowStockCount;
            ViewBag.OutOfStockCount = summary.OutOfStockCount;
            ViewBag.TotalValue      = summary.TotalValue;
            ViewBag.CategoryCount   = summary.CategoryCount;
            
            ViewBag.Search          = search;
            ViewBag.Filter          = filter;

            return View(summary.Products);
        }

        // Hiển thị form thêm mới sản phẩm
        public async Task<IActionResult> Create()
        {
            ViewData["CategoryId"] = await _productService.GetCategorySelectList();
            return View();
        }

        // Xử lý thêm mới sản phẩm
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            [Bind("Sku,Name,CategoryId,Unit,Stock,MinStock,ExpiryDate,ShelfLocation,Price,CostPrice,ImageUrl,AnimalType,Description")]
            Product product,
            IFormFile? ImageFile)
        {
            if (ImageFile != null && ImageFile.Length > 0)
            {
                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
                var extension = Path.GetExtension(ImageFile.FileName).ToLowerInvariant();
                if (!allowedExtensions.Contains(extension))
                {
                    ModelState.AddModelError("ImageUrl", "Chỉ chấp nhận các định dạng ảnh (.jpg, .jpeg, .png, .gif, .webp).");
                }
                else
                {
                    var safeSku = string.Concat(product.Sku.Split(Path.GetInvalidFileNameChars())).Trim().ToLowerInvariant();
                    var uniqueFileName = $"{safeSku}_{DateTime.Now.Ticks}{extension}";
                    var uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "images", "products");

                    if (!Directory.Exists(uploadsFolder))
                    {
                        Directory.CreateDirectory(uploadsFolder);
                    }

                    var filePath = Path.Combine(uploadsFolder, uniqueFileName);
                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        await ImageFile.CopyToAsync(fileStream);
                    }

                    product.ImageUrl = $"/images/products/{uniqueFileName}";
                }
            }

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

        // Hiển thị form chỉnh sửa sản phẩm
        public async Task<IActionResult> Edit(string id)
        {
            if (id == null) return NotFound();

            var product = await _productService.GetProductBySku(id);
            if (product == null) return NotFound();

            ViewData["CategoryId"] = await _productService.GetCategorySelectList(product.CategoryId);
            return View(product);
        }

        // Xử lý cập nhật sản phẩm
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            string id,
            [Bind("Sku,Name,CategoryId,Unit,MinStock,ExpiryDate,ShelfLocation,Price,CostPrice,ImageUrl,AnimalType,Description")]
            Product productUpdate,
            IFormFile? ImageFile)
        {
            if (id != productUpdate.Sku)
            {
                return NotFound();
            }

            // Lấy lại Stock hiện tại để không bị ghi đè và để hiển thị lại nếu có lỗi validate
            var existingProduct = await _productService.GetProductBySku(id);
            if (existingProduct != null)
            {
                productUpdate.Stock = existingProduct.Stock;
            }

            if (ImageFile != null && ImageFile.Length > 0)
            {
                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
                var extension = Path.GetExtension(ImageFile.FileName).ToLowerInvariant();
                if (!allowedExtensions.Contains(extension))
                {
                    ModelState.AddModelError("ImageUrl", "Chỉ chấp nhận các định dạng ảnh (.jpg, .jpeg, .png, .gif, .webp).");
                }
                else
                {
                    // Delete old local image if exists
                    if (!string.IsNullOrEmpty(productUpdate.ImageUrl) && productUpdate.ImageUrl.StartsWith("/images/products/"))
                    {
                        var oldFilePath = Path.Combine(_webHostEnvironment.WebRootPath, productUpdate.ImageUrl.TrimStart('/'));
                        if (System.IO.File.Exists(oldFilePath))
                        {
                            System.IO.File.Delete(oldFilePath);
                        }
                    }

                    var safeSku = string.Concat(productUpdate.Sku.Split(Path.GetInvalidFileNameChars())).Trim().ToLowerInvariant();
                    var uniqueFileName = $"{safeSku}_{DateTime.Now.Ticks}{extension}";
                    var uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "images", "products");

                    if (!Directory.Exists(uploadsFolder))
                    {
                        Directory.CreateDirectory(uploadsFolder);
                    }

                    var filePath = Path.Combine(uploadsFolder, uniqueFileName);
                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        await ImageFile.CopyToAsync(fileStream);
                    }

                    productUpdate.ImageUrl = $"/images/products/{uniqueFileName}";
                }
            }

            if (!ModelState.IsValid)
            {
                ViewData["CategoryId"] = await _productService.GetCategorySelectList(productUpdate.CategoryId);
                return View(productUpdate);
            }

            try
            {
                await _productService.UpdateProduct(id, productUpdate);
                return RedirectToAction(nameof(Index));
            }
            catch (ServiceException ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
                ViewData["CategoryId"] = await _productService.GetCategorySelectList(productUpdate.CategoryId);
                return View(productUpdate);
            }
        }

        // Xử lý xóa (hoặc ẩn) sản phẩm
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(string id)
        {
            await _productService.DeleteProduct(id);
            return RedirectToAction(nameof(Index));
        }

        // Xử lý khôi phục sản phẩm đã xóa
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Restore(string id)
        {
            await _productService.RestoreProduct(id);
            return RedirectToAction(nameof(Index), new { filter = "deleted" });
        }
    }
}
