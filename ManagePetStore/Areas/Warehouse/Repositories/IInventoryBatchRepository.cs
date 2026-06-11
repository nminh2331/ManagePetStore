/**
 * Project: Pet Store Management System (PSMS)
 * File: IInventoryBatchRepository.cs
 * Author: Tran Duong
 * Date: June 10, 2026
 * Description: Giao diện truy xuất dữ liệu cho bảng lô hàng (InventoryBatch).
 */
using ManagePetStore.Models;

namespace ManagePetStore.Areas.Warehouse.Repositories;

public interface IInventoryBatchRepository
{
    /// Lấy tất cả lô hàng của một sản phẩm theo SKU.
    Task<IEnumerable<InventoryBatch>> GetBatchesByProductSku(string productSku);

    /// Lấy một lô hàng theo BatchId.
    Task<InventoryBatch?> GetBatchById(int batchId);

    /// Thêm lô hàng mới.
    Task AddBatch(InventoryBatch batch);

    /// Cập nhật lô hàng.
    Task UpdateBatch(InventoryBatch batch);

    /// Xóa lô hàng theo BatchId.
    Task DeleteBatch(int batchId);
}
