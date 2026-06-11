/**
 * Project: Pet Store Management System (PSMS)
 * File: InventoryBatchController.cs
 * Author: Tran Duong
 * Date: June 10, 2026
 * Description: Controller xử lý lô hàng.
 */
using ManagePetStore.Areas.Warehouse.Services;
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

        public InventoryBatchController(IInventoryBatchService batchService, IProductService productService)
        {
            _batchService = batchService;
            _productService = productService;
        }

        // GET: Warehouse/InventoryBatch/Index/{productSku}
        public async Task<IActionResult> Index(string id)
        {
            if (string.IsNullOrEmpty(id)) return NotFound();

            var product = await _productService.GetProductBySku(id);
            if (product == null) return NotFound();

            ViewBag.Product = product;
            var batches = await _batchService.GetBatchesByProductSku(id);

            return View(batches);
        }

        // GET: Warehouse/InventoryBatch/Create?productSku={sku}
        public async Task<IActionResult> Create(string productSku)
        {
            if (string.IsNullOrEmpty(productSku)) return NotFound();

            var product = await _productService.GetProductBySku(productSku);
            if (product == null) return NotFound();

            ViewBag.Product = product;
            return View(new InventoryBatch { ProductSku = productSku, ReceivedDate = DateTime.Now });
        }

        // POST: Warehouse/InventoryBatch/Create
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

        // GET: Warehouse/InventoryBatch/Edit/{id}
        public async Task<IActionResult> Edit(int id)
        {
            var batch = await _batchService.GetBatchById(id);
            if (batch == null) return NotFound();

            var product = await _productService.GetProductBySku(batch.ProductSku);
            ViewBag.Product = product;

            return View(batch);
        }

        // POST: Warehouse/InventoryBatch/Edit/{id}
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

        // POST: Warehouse/InventoryBatch/Delete/{id}
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
    }
}
