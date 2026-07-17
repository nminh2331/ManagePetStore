/**
 * Project: Pet Store Management System (PSMS)
 * File: InventoryBatchController.cs
 * Author: Tran Duong
 * Date: June 10, 2026
 * Last Update: July 17, 2026
 * Description: Controller xá»­ lÃ½ lÃ´ hÃ ng.
 */
using ManagePetStore.Services.Warehouse;
using ManagePetStore.Repositories.Warehouse;
using ManagePetStore.Exceptions;
using ManagePetStore.Models;
using ManagePetStore.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ManagePetStore.Areas.Warehouse.Controllers
{
    [Area("Warehouse")]
    [Authorize(Roles = "warehouse,admin")]
    public class InventoryBatchController : Controller
    {
        private readonly IInventoryBatchService _batchService;
        private readonly IProductService _productService;
        private readonly IStockMovementRepository _movementRepo;

        public InventoryBatchController(
            IInventoryBatchService batchService, 
            IProductService productService,
            IStockMovementRepository movementRepo)
        {
            _batchService = batchService;
            _productService = productService;
            _movementRepo = movementRepo;
        }

        // Hiá»ƒn thá»‹ danh sÃ¡ch lÃ´ hÃ ng cá»§a má»™t sáº£n pháº©m
        public async Task<IActionResult> Index(string id)
        {
            if (string.IsNullOrEmpty(id)) return NotFound();

            var product = await _productService.GetProductBySku(id);
            if (product == null) return NotFound();

            ViewBag.Product = product;
            var batches = await _batchService.GetBatchesByProductSku(id);

            return View(batches);
        }

        // Hiá»ƒn thá»‹ form thÃªm má»›i lÃ´ hÃ ng cho má»™t sáº£n pháº©m
        public async Task<IActionResult> Create(string productSku)
        {
            if (string.IsNullOrEmpty(productSku)) return NotFound();

            var product = await _productService.GetProductBySku(productSku);
            if (product == null) return NotFound();

            ViewBag.Product = product;
            return View(new InventoryBatch { ProductSku = productSku, ReceivedDate = DateTime.Now });
        }

        // Xá»­ lÃ½ thÃªm má»›i lÃ´ hÃ ng
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("ProductSku,Quantity,ExpiryDate")] InventoryBatch batch)
        {
            var product = await _productService.GetProductBySku(batch.ProductSku);
            if (product == null) return NotFound();

            if (!ModelState.IsValid)
            {
                ViewBag.Product = product;
                return View(batch);
            }

            try
            {
                await _batchService.CreateBatch(batch);
                return RedirectToAction(nameof(Index), new { id = batch.ProductSku });
            }
            catch (ServiceException ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
                ViewBag.Product = product;
                return View(batch);
            }
        }

        // Hiá»ƒn thá»‹ form chá»‰nh sá»­a thÃ´ng tin lÃ´ hÃ ng
        public async Task<IActionResult> Edit(int id)
        {
            var batch = await _batchService.GetBatchById(id);
            if (batch == null) return NotFound();

            var product = await _productService.GetProductBySku(batch.ProductSku);
            ViewBag.Product = product;

            return View(batch);
        }

        // Xá»­ lÃ½ cáº­p nháº­t thÃ´ng tin lÃ´ hÃ ng
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("BatchId,ProductSku,CurrentQuantity,ExpiryDate")] InventoryBatch batchUpdate)
        {
            if (id != batchUpdate.BatchId) return NotFound();

            try
            {
                await _batchService.UpdateBatch(batchUpdate.BatchId, batchUpdate.CurrentQuantity, batchUpdate.ExpiryDate);
                return RedirectToAction(nameof(Index), new { id = batchUpdate.ProductSku });
            }
            catch (ServiceException ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
                var product = await _productService.GetProductBySku(batchUpdate.ProductSku);
                ViewBag.Product = product;
                return View(batchUpdate);
            }
        }

        // Xá»­ lÃ½ xÃ³a lÃ´ hÃ ng
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var batch = await _batchService.GetBatchById(id);
            if (batch == null) return NotFound();

            string sku = batch.ProductSku;
            await _batchService.DeleteBatch(id);
            
            return RedirectToAction(nameof(Index), new { id = sku });
        }

        // Xá»­ lÃ½ Ä‘á»“ng bá»™ sá»‘ lÆ°á»£ng tá»“n kho ban Ä‘áº§u (táº¡o lÃ´ hÃ ng Ä‘iá»u chá»‰nh)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SyncStock(string productSku)
        {
            var product = await _productService.GetProductBySku(productSku);
            if (product == null) return NotFound();

            var batches = await _batchService.GetBatchesByProductSku(productSku);
            int totalBatchStock = batches.Sum(b => b.CurrentQuantity);
            
            if (product.Stock > totalBatchStock)
            {
                int diff = product.Stock - totalBatchStock;
                var adjustmentBatch = new InventoryBatch
                {
                    ProductSku = productSku,
                    Quantity = diff,
                    CurrentQuantity = diff,
                    ReceivedDate = DateTime.Now,
                    ExpiryDate = DateTime.Now.AddYears(1) // Máº·c Ä‘á»‹nh 1 nÄƒm
                };
                
                // Trá»±c tiáº¿p dÃ¹ng Repo hoáº·c gÃ¡n qua Service.
                // VÃ¬ CreateBatch trong Service cÃ³ cá»™ng thÃªm Stock, mÃ  ta chá»‰ muá»‘n Ä‘iá»u chá»‰nh Batch nÃªn pháº£i lÆ°u Ã½.
                // á»ž Ä‘Ã¢y ta cÃ³ thá»ƒ táº¡m giáº£m Stock cá»§a Product Ä‘i (Ä‘á»ƒ CreateBatch cá»™ng láº¡i lÃ  vá»«a), 
                // hoáº·c táº¡o batch mÃ  bá» qua cá»™ng Stock. Ta sáº½ trá»« Ä‘i trÆ°á»›c.
                product.Stock -= diff;
                await _productService.UpdateProduct(productSku, product);
                
                await _batchService.CreateBatch(adjustmentBatch);

                // Ghi láº¡i lá»‹ch sá»­ (StockMovement)
                var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier);
                int userId = userIdClaim != null && int.TryParse(userIdClaim.Value, out int parsedId) ? parsedId : 1;

                var movement = new StockMovement
                {
                    Type = "Nháº­p hÃ ng", 
                    Status = "HoÃ n thÃ nh", 
                    Supplier = "Äá»“ng bá»™ sá»‘ lÆ°á»£ng tá»“n kho (Tá»± Ä‘á»™ng)",
                    CreatedById = userId,
                    Date = DateTime.Now,
                    TotalValue = 0,
                    StockMovementDetails = new List<StockMovementDetail>
                    {
                        new StockMovementDetail
                        {
                            ProductSku = productSku,
                            Quantity = diff,
                            CostPrice = 0
                        }
                    }
                };
                await _movementRepo.AddMovement(movement);
            }
            
            return RedirectToAction(nameof(Index), new { id = productSku });
        }
    }
}
