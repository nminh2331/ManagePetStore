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
    
    // Hủy / Xóa lô hàng
    Task DeleteBatch(int batchId);

    // Xuất kho FIFO
    Task DeductStockFIFO(string productSku, int quantityToDeduct);

    // Nhập kho lại (hoàn trả) vào các lô cũ
    Task RestockToBatches(string productSku, int quantityToRestock);
}
