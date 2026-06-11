/**
 * Project: Pet Store Management System (PSMS)
 * File: IInventoryBatchService.cs
 * Author: Tran Duong
 * Date: June 10, 2026
 * Description: Giao diện dịch vụ quản lý lô hàng.
 */
using ManagePetStore.Models;

namespace ManagePetStore.Areas.Warehouse.Services;

public interface IInventoryBatchService
{
    Task<IEnumerable<InventoryBatch>> GetBatchesByProductSku(string productSku);
    Task<InventoryBatch?> GetBatchById(int batchId);
    Task CreateBatch(InventoryBatch batch);
    Task UpdateBatch(int batchId, int newQuantity, DateTime newExpiryDate);
    Task DeleteBatch(int batchId);
}
