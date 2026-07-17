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
    
    // Há»§y / XÃ³a lÃ´ hÃ ng
    Task DeleteBatch(int batchId);

    // Xuáº¥t kho FIFO
    Task DeductStockFIFO(string productSku, int quantityToDeduct);

    // Nháº­p kho láº¡i (hoÃ n tráº£) vÃ o cÃ¡c lÃ´ cÅ©
    Task RestockToBatches(string productSku, int quantityToRestock);
}
