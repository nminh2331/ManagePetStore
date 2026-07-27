/**
 * Project: Pet Store Management System (PSMS)
 * File: IInventoryBatchService.cs
 * Author: Tran Duong
 * Date: June 10, 2026
 * Last Update: July 17, 2026
 * Description: Giao diá»‡n dá»‹ch vá»¥ quáº£n lÃ½ lÃ´ hÃ ng.
 */
using ManagePetStore.Models;

namespace ManagePetStore.Services.Warehouse;

public interface IInventoryBatchService
{
    Task<IEnumerable<InventoryBatch>> GetBatchesByProductSku(string productSku);
    Task<InventoryBatch?> GetBatchById(int batchId);
    Task CreateBatch(InventoryBatch batch);
    Task UpdateBatch(int batchId, int newQuantity, DateTime newExpiryDate);
    Task AdjustBatchQuantityAsync(int batchId, int quantityDelta);

    // Xuáº¥t kho FIFO
    Task DeductStockFIFO(string productSku, int quantityToDeduct);

    // Nhập kho lại (hoàn trả) vào các lô cũ
    Task RestockToBatches(string productSku, int quantityToRestock);

    // Lấy các lô hàng sắp hết hạn
    Task<IEnumerable<InventoryBatch>> GetExpiringBatches(int daysThreshold = 30);
}
