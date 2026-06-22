/**
 * Project: Pet Store Management System (PSMS)
 * File: IInventoryBatchService.cs
 * Author: Tran Duong
 * Date: June 10, 2026
 * Description: Giao diện dịch vụ quản lý lô hàng.
 */
using ManagePetStore.Models;

namespace ManagePetStore.Services.Warehouse;

public interface IInventoryBatchService
{
    Task<IEnumerable<InventoryBatch>> GetBatchesByProductSku(string productSku);
    Task<InventoryBatch?> GetBatchById(int batchId);
    Task CreateBatch(InventoryBatch batch);
    Task UpdateBatch(int batchId, int newQuantity, DateTime newExpiryDate);
    Task DeleteBatch(int batchId);
    
    // Thuật toán trừ tồn kho theo nguyên tắc FIFO (vào trước ra trước)
    Task DeductStockFIFO(string productSku, int quantityToDeduct);
}
