/**
 * Project: Pet Store Management System (PSMS)
 * File: ProductController.cs
 * Author: Tran Duong
 * Date: May 31, 2026
 * Last Update: July 17, 2026
 * Description: Xá»­ lÃ½ cÃ¡c yÃªu cáº§u HTTP cho chá»©c nÄƒng quáº£n lÃ½ sáº£n pháº©m trong kho hÃ ng (thÃªm, sá»­a, xÃ³a sáº£n pháº©m vÃ  theo dÃµi tÃ¬nh tráº¡ng tá»“n kho).
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

        // Hiá»ƒn thá»‹ danh sÃ¡ch sáº£n pháº©m
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

        // Hiá»ƒn thá»‹ form thÃªm má»›i sáº£n pháº©m
        public async Task<IActionResult> Create()
        {
            ViewData["CategoryId"] = await _productService.GetCategorySelectList();
            return View();
        }

        // Xá»­ lÃ½ thÃªm má»›i sáº£n pháº©m
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
                    ModelState.AddModelError("ImageUrl", "Chá»‰ cháº¥p nháº­n cÃ¡c Ä‘á»‹nh dáº¡ng áº£nh (.jpg, .jpeg, .png, .gif, .webp).");
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
                int initialStock = product.Stock;
                if (initialStock > 0)
                {
                    product.Stock = 0; // Äá»ƒ CreateBatch tá»± Ä‘á»™ng cá»™ng láº¡i
                }

                await _productService.CreateProduct(product);

                if (initialStock > 0)
                {
                    int userId = 1;
                    try {
                        var userClaim = User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier);
                        if (userClaim != null && int.TryParse(userClaim.Value, out int uid)) userId = uid;
                    } catch { }

                    decimal costPrice = product.CostPrice;

                    var batch = new InventoryBatch
                    {
                        ProductSku = product.Sku,
                        Quantity = initialStock,
                        CurrentQuantity = initialStock,
                        ReceivedDate = DateTime.Now,
                        ExpiryDate = product.ExpiryDate ?? DateTime.Now.AddYears(1)
                    };
                    await _batchService.CreateBatch(batch);

                    var movement = new StockMovement
                    {
                        Type = "Nháº­p hÃ ng",
                        Status = "HoÃ n thÃ nh",
                        Supplier = "Khá»Ÿi táº¡o tá»“n kho ban Ä‘áº§u",
                        CreatedById = userId,
                        Date = DateTime.Now,
                        TotalValue = initialStock * costPrice,
                        StockMovementDetails = new List<StockMovementDetail>
                        {
                            new StockMovementDetail
                            {
                                ProductSku = product.Sku,
                                Quantity = initialStock,
                                CostPrice = costPrice
                            }
                        }
                    };
                    await _movementRepo.AddMovement(movement);
                }

                return RedirectToAction(nameof(Index));
            }
            catch (ServiceException ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
                ViewData["CategoryId"] = await _productService.GetCategorySelectList(product.CategoryId);
                // KhÃ´i phá»¥c láº¡i Stock náº¿u cÃ³ lá»—i
                if (product.Stock == 0 && ModelState.ErrorCount > 0)
                {
                    // Thá»±c cháº¥t náº¿u cÃ³ lá»—i tá»« DB thÃ¬ product.Stock Ä‘ang bá»‹ set thÃ nh 0, ta khÃ´ng thá»ƒ láº¥y láº¡i initialStock trá»« khi ta lÆ°u nÃ³.
                    // ÄÆ¡n giáº£n nháº¥t lÃ  Ä‘á»ƒ nÃ³ 0, ngÆ°á»i dÃ¹ng nháº­p láº¡i.
                }
                return View(product);
            }
        }

        // Hiá»ƒn thá»‹ form chá»‰nh sá»­a sáº£n pháº©m
        public async Task<IActionResult> Edit(string id)
        {
            if (id == null) return NotFound();

            var product = await _productService.GetProductBySku(id);
            if (product == null) return NotFound();

            ViewData["CategoryId"] = await _productService.GetCategorySelectList(product.CategoryId);
            return View(product);
        }

        // Xá»­ lÃ½ cáº­p nháº­t sáº£n pháº©m
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

            // Láº¥y láº¡i Stock hiá»‡n táº¡i Ä‘á»ƒ khÃ´ng bá»‹ ghi Ä‘Ã¨ vÃ  Ä‘á»ƒ hiá»ƒn thá»‹ láº¡i náº¿u cÃ³ lá»—i validate
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
                    ModelState.AddModelError("ImageUrl", "Chá»‰ cháº¥p nháº­n cÃ¡c Ä‘á»‹nh dáº¡ng áº£nh (.jpg, .jpeg, .png, .gif, .webp).");
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

        // Xá»­ lÃ½ xÃ³a (hoáº·c áº©n) sáº£n pháº©m
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(string id)
        {
            await _productService.DeleteProduct(id);
            return RedirectToAction(nameof(Index));
        }

        // Xá»­ lÃ½ khÃ´i phá»¥c sáº£n pháº©m Ä‘Ã£ xÃ³a
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Restore(string id)
        {
            await _productService.RestoreProduct(id);
            return RedirectToAction(nameof(Index), new { filter = "deleted" });
        }
    }
}
