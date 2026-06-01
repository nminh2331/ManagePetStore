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
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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

        public ProductController(
            IProductService productService,
            IWebHostEnvironment webHostEnvironment)
        {
            _productService = productService;
            _webHostEnvironment = webHostEnvironment;
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
                    // Delete old local image if exists
                    if (!string.IsNullOrEmpty(product.ImageUrl) && product.ImageUrl.StartsWith("/images/products/"))
                    {
                        var oldFilePath = Path.Combine(_webHostEnvironment.WebRootPath, product.ImageUrl.TrimStart('/'));
                        if (System.IO.File.Exists(oldFilePath))
                        {
                            System.IO.File.Delete(oldFilePath);
                        }
                    }

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
            // Get product to check if it has a local image to delete
            var product = await _productService.GetProductBySku(id);
            if (product != null && !string.IsNullOrEmpty(product.ImageUrl) && product.ImageUrl.StartsWith("/images/products/"))
            {
                var filePath = Path.Combine(_webHostEnvironment.WebRootPath, product.ImageUrl.TrimStart('/'));
                if (System.IO.File.Exists(filePath))
                {
                    System.IO.File.Delete(filePath);
                }
            }

            await _productService.DeleteProduct(id);
            return RedirectToAction(nameof(Index));
        }
    }
}
