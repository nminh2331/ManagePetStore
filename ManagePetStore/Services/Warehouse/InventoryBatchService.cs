/**
 * Project: Pet Store Management System (PSMS)
 * File: InventoryBatchService.cs
 * Author: Tran Duong
 * Date: June 10, 2026
 * Last Update: July 20, 2026
 * Description: Triển khai dịch vụ quản lý lô hàng và đồng bộ tồn kho an toàn (Atomic).
 */
using ManagePetStore.Repositories.Warehouse;
using ManagePetStore.Exceptions;
using ManagePetStore.Models;
using ManagePetStore.Repositories;

namespace ManagePetStore.Services.Warehouse;

public class InventoryBatchService : IInventoryBatchService
{
    private readonly IInventoryBatchRepository _batchRepo;
    private readonly IProductRepository _productRepo;

    public InventoryBatchService(
        IInventoryBatchRepository batchRepo,
        IProductRepository productRepo)
    {
        _batchRepo = batchRepo;
        _productRepo = productRepo;
    }

    public async Task<IEnumerable<InventoryBatch>> GetBatchesByProductSku(string productSku)
    {
        return await _batchRepo.GetBatchesByProductSku(productSku);
    }

    public async Task<InventoryBatch?> GetBatchById(int batchId)
    {
        return await _batchRepo.GetBatchById(batchId);
    }

    public async Task CreateBatch(InventoryBatch batch)
    {
        if (!await _productRepo.ProductExists(batch.ProductSku))
            throw new ServiceException($"Không tìm thấy sản phẩm mã '{batch.ProductSku}'.");

        batch.ReceivedDate = DateTime.Now;
        if (batch.Quantity < 0)
            throw new ServiceException("Số lượng nhập không được âm.");
        
        batch.CurrentQuantity = batch.Quantity; // Khi mới nhập, số lượng hiện tại bằng số lượng nhập

        await _batchRepo.AddBatch(batch);

        // Đồng bộ Stock nguyên tử
        await _productRepo.AdjustStockAsync(batch.ProductSku, batch.CurrentQuantity);
    }

    public async Task UpdateBatch(int batchId, int newQuantity, DateTime newExpiryDate)
    {
        var batch = await _batchRepo.GetBatchById(batchId);
        if (batch == null)
            throw new ServiceException("Không tìm thấy lô hàng.");

        if (!await _productRepo.ProductExists(batch.ProductSku))
            throw new ServiceException("Sản phẩm của lô hàng không tồn tại.");

        if (newQuantity < 0)
            throw new ServiceException("Số lượng không được âm.");

        // Tính chênh lệch để cập nhật vào Product
        int diff = newQuantity - batch.CurrentQuantity;

        batch.CurrentQuantity = newQuantity;
        batch.ExpiryDate = newExpiryDate;

        await _batchRepo.UpdateBatch(batch); // Cập nhật lô thủ công

        // Đồng bộ Stock nguyên tử
        await _productRepo.AdjustStockAsync(batch.ProductSku, diff);
    }

    public async Task AdjustBatchQuantityAsync(int batchId, int quantityDelta)
    {
        var batch = await _batchRepo.GetBatchById(batchId);
        if (batch == null)
            throw new ServiceException("Không tìm thấy lô hàng.");
            
        int affected = await _batchRepo.AdjustBatchQuantityAsync(batchId, quantityDelta);
        if (affected == 0)
            throw new ServiceException("Số lượng lô hàng không đủ hoặc đã bị thay đổi bởi người khác.");
            
        await _productRepo.AdjustStockAsync(batch.ProductSku, quantityDelta);
    }

    public async Task DeleteBatch(int batchId)
    {
        var batch = await _batchRepo.GetBatchById(batchId);
        if (batch != null)
        {
            if (await _productRepo.ProductExists(batch.ProductSku))
            {
                await _productRepo.AdjustStockAsync(batch.ProductSku, -batch.CurrentQuantity);
            }

            await _batchRepo.DeleteBatch(batchId);
        }
    }

    public async Task DeductStockFIFO(string productSku, int quantityToDeduct)
    {
        if (!await _productRepo.ProductExists(productSku)) 
            throw new ServiceException("Sản phẩm không tồn tại.");

        if (quantityToDeduct <= 0) return;

        int remainingToDeduct = quantityToDeduct;
        int maxRetries = 10;
        int retryCount = 0;

        while (remainingToDeduct > 0 && retryCount < maxRetries)
        {
            var batches = (await _batchRepo.GetBatchesByProductSku(productSku))
                .Where(b => b.CurrentQuantity > 0)
                .OrderBy(b => b.ReceivedDate) // Cũ nhất xuất trước
                .ToList();

            if (!batches.Any())
            {
                throw new ServiceException("Không đủ tồn kho.");
            }

            foreach (var batch in batches)
            {
                if (remainingToDeduct <= 0) break;

                int deductAmount = Math.Min(batch.CurrentQuantity, remainingToDeduct);

                // Thử trừ số lượng lô hàng một cách nguyên tử
                int affectedRows = await _batchRepo.AdjustBatchQuantityAsync(batch.BatchId, -deductAmount);
                
                if (affectedRows > 0)
                {
                    // Trừ thành công
                    remainingToDeduct -= deductAmount;
                }
                else
                {
                    // Lô hàng đã bị thay đổi (Race Condition), break vòng lặp con để lấy lại danh sách lô hàng mới
                    break;
                }
            }
            
            retryCount++;
        }

        if (remainingToDeduct > 0)
        {
            throw new ServiceException($"Không đủ tồn kho để xuất. Hệ thống đang gặp nghẽn, vui lòng thử lại sau.");
        }

        // Cập nhật tổng tồn kho nguyên tử
        await _productRepo.AdjustStockAsync(productSku, -quantityToDeduct);
    }

    public async Task RestockToBatches(string productSku, int quantityToRestock)
    {
        if (!await _productRepo.ProductExists(productSku)) 
            throw new ServiceException("Sản phẩm không tồn tại.");

        if (quantityToRestock <= 0) return;

        // Tìm lô hàng nhập vào gần nhất
        var newestBatch = (await _batchRepo.GetBatchesByProductSku(productSku))
            .OrderByDescending(b => b.ReceivedDate)
            .FirstOrDefault();

        if (newestBatch != null)
        {
            await _batchRepo.AdjustBatchQuantityAsync(newestBatch.BatchId, quantityToRestock);
        }

        // Cập nhật tổng tồn kho
        await _productRepo.AdjustStockAsync(productSku, quantityToRestock);
    }
}
