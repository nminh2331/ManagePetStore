/**
 * Project: Pet Store Management System (PSMS)
 * File: InventoryBatchController.cs
 * Author: Tran Duong
 * Date: June 10, 2026
 * Last Update: July 23, 2026
 * Description: Controller xử lý lô hàng.
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
        private readonly IInventoryBatchRepository _batchRepo;
        private readonly IProductService _productService;
        private readonly IStockMovementRepository _movementRepo;

        public InventoryBatchController(
            IInventoryBatchService batchService,
            IInventoryBatchRepository batchRepo,
            IProductService productService,
            IStockMovementRepository movementRepo)
        {
            _batchService = batchService;
            _batchRepo = batchRepo;
            _productService = productService;
            _movementRepo = movementRepo;
        }

        // Hiển thị danh sách lô hàng của một sản phẩm
        public async Task<IActionResult> Index(string id)
        {
            if (string.IsNullOrEmpty(id)) return NotFound();

            var product = await _productService.GetProductBySku(id);
            if (product == null) return NotFound();

            ViewBag.Product = product;
            var batches = await _batchService.GetBatchesByProductSku(id);

            return View(batches);
        }

        // Hiển thị form thêm mới lô hàng cho một sản phẩm
        public async Task<IActionResult> Create(string productSku)
        {
            if (string.IsNullOrEmpty(productSku)) return NotFound();

            var product = await _productService.GetProductBySku(productSku);
            if (product == null) return NotFound();

            ViewBag.Product = product;
            return View(new InventoryBatch { ProductSku = productSku, ReceivedDate = DateTime.Now });
        }

        // Xử lý thêm mới lô hàng
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

        // Hiển thị form chỉnh sửa thông tin lô hàng
        public async Task<IActionResult> Edit(int id)
        {
            var batch = await _batchService.GetBatchById(id);
            if (batch == null) return NotFound();

            var product = await _productService.GetProductBySku(batch.ProductSku);
            ViewBag.Product = product;

            return View(batch);
        }

        // Xử lý cập nhật thông tin lô hàng
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

        // Xử lý xóa lô hàng
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

        // Đồng bộ tồn kho: Cập nhật Product.Stock (số lượng bên ngoài) cho khớp với tổng số lượng trong các lô hàng.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SyncStock(string productSku)
        {
            var product = await _productService.GetProductBySku(productSku);
            if (product == null) return NotFound();

            var batches = await _batchService.GetBatchesByProductSku(productSku);
            int totalBatchStock = batches.Sum(b => b.CurrentQuantity);

            if (product.Stock != totalBatchStock)
            {
                int oldStock = product.Stock;
                product.Stock = totalBatchStock;
                await _productService.UpdateProduct(productSku, product);

                if (oldStock > totalBatchStock)
                {
                    TempData["SuccessMessage"] = $"Đã giảm tồn kho từ {oldStock} xuống {totalBatchStock} để khớp với dữ liệu thực tế ở các lô hàng.";
                }
                else
                {
                    TempData["SuccessMessage"] = $"Đã cập nhật tồn kho từ {oldStock} lên {totalBatchStock} để khớp với các lô hàng.";
                }
            }
            else
            {
                TempData["SuccessMessage"] = "Tồn kho đã khớp với dữ liệu lô hàng, không cần đồng bộ.";
            }

            return RedirectToAction(nameof(Index), new { id = productSku });
        }
    }
}
