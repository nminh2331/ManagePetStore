/**
 * Project: Pet Store Management System (PSMS)
 * File: InventoryBatchController.cs
 * Author: Tran Duong
 * Date: June 10, 2026
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

        // Xử lý đồng bộ số lượng tồn kho ban đầu (tạo lô hàng điều chỉnh)
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
                    ExpiryDate = DateTime.Now.AddYears(1) // Mặc định 1 năm
                };
                
                // Trực tiếp dùng Repo hoặc gán qua Service.
                // Vì CreateBatch trong Service có cộng thêm Stock, mà ta chỉ muốn điều chỉnh Batch nên phải lưu ý.
                // Ở đây ta có thể tạm giảm Stock của Product đi (để CreateBatch cộng lại là vừa), 
                // hoặc tạo batch mà bỏ qua cộng Stock. Ta sẽ trừ đi trước.
                product.Stock -= diff;
                await _productService.UpdateProduct(productSku, product);
                
                await _batchService.CreateBatch(adjustmentBatch);

                // Ghi lại lịch sử (StockMovement)
                var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier);
                int userId = userIdClaim != null && int.TryParse(userIdClaim.Value, out int parsedId) ? parsedId : 1;

                var movement = new StockMovement
                {
                    Type = "Nhập hàng", 
                    Status = "Hoàn thành", 
                    Supplier = "Đồng bộ số lượng tồn kho (Tự động)",
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
