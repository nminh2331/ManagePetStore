/**
 * Project: Pet Store Management System (PSMS)
 * File: IStockMovementRepository.cs
 * Author: Tran Duong
 * Date: June 11, 2026
 * Description: Giao diện truy xuất dữ liệu cho Phiếu Xuất/Nhập kho (StockMovement).
 */
using ManagePetStore.Models;

namespace ManagePetStore.Repositories.Warehouse;

public interface IStockMovementRepository
{
    Task<IEnumerable<StockMovement>> GetAllMovements(DateTime? fromDate = null, DateTime? toDate = null, string? search = null);
    Task<StockMovement?> GetMovementById(int id);
    Task AddMovement(StockMovement movement);
    Task UpdateMovement(StockMovement movement);
    
    // Tìm phiếu nhập đang chờ duyệt cho một sản phẩm cụ thể
    Task<StockMovement?> GetPendingImportByProduct(string productSku);
}
