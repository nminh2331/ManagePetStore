/**
 * Project: Pet Store Management System (PSMS)
 * File: InventoryBatchService.cs
 * Author: Tran Duong
 * Date: June 10, 2026
 * Description: Triển khai dịch vụ quản lý lô hàng và đồng bộ số lượng tồn kho với Sản phẩm.
 */
using ManagePetStore.Areas.Warehouse.Repositories;
using ManagePetStore.Exceptions;
using ManagePetStore.Models;
using ManagePetStore.Repositories;

namespace ManagePetStore.Areas.Warehouse.Services;

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
        var product = await _productRepo.GetProductBySku(batch.ProductSku);
        if (product == null)
            throw new ServiceException($"Không tìm thấy sản phẩm mã '{batch.ProductSku}'.");

        batch.ReceivedDate = DateTime.Now;
        if (batch.Quantity < 0)
            throw new ServiceException("Số lượng nhập không được âm.");
        
        batch.CurrentQuantity = batch.Quantity; // Khi mới nhập, số lượng hiện tại bằng số lượng nhập

        await _batchRepo.AddBatch(batch);

        // Đồng bộ Stock của Product
        product.Stock += batch.CurrentQuantity;
        await _productRepo.UpdateProduct(product);
    }

    public async Task UpdateBatch(int batchId, int newQuantity, DateTime newExpiryDate)
    {
        var batch = await _batchRepo.GetBatchById(batchId);
        if (batch == null)
            throw new ServiceException("Không tìm thấy lô hàng.");

        var product = await _productRepo.GetProductBySku(batch.ProductSku);
        if (product == null)
            throw new ServiceException("Sản phẩm của lô hàng không tồn tại.");

        if (newQuantity < 0)
            throw new ServiceException("Số lượng không được âm.");

        // Tính chênh lệch để cập nhật vào Product
        int diff = newQuantity - batch.CurrentQuantity;

        batch.CurrentQuantity = newQuantity;
        batch.ExpiryDate = newExpiryDate;

        await _batchRepo.UpdateBatch(batch);

        // Đồng bộ Stock của Product
        product.Stock += diff;
        if (product.Stock < 0) product.Stock = 0; // Đảm bảo an toàn
        await _productRepo.UpdateProduct(product);
    }

    public async Task DeleteBatch(int batchId)
    {
        var batch = await _batchRepo.GetBatchById(batchId);
        if (batch != null)
        {
            var product = await _productRepo.GetProductBySku(batch.ProductSku);
            if (product != null)
            {
                product.Stock -= batch.CurrentQuantity;
                if (product.Stock < 0) product.Stock = 0;
                await _productRepo.UpdateProduct(product);
            }

            await _batchRepo.DeleteBatch(batchId);
        }
    }
}
