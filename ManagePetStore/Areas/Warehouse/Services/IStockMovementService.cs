/**
 * Project: Pet Store Management System (PSMS)
 * File: IStockMovementService.cs
 * Author: Tran Duong
 * Date: June 11, 2026
 * Description: Giao diện dịch vụ xử lý nghiệp vụ Nhập/Xuất kho.
 */
using ManagePetStore.Models;

namespace ManagePetStore.Areas.Warehouse.Services;

public interface IStockMovementService
{
    Task<IEnumerable<StockMovement>> GetAllMovements(DateTime? fromDate = null, DateTime? toDate = null);
    Task<StockMovement?> GetMovementById(int id);
    
    // Tạo đơn nhập hàng (Purchase Order)
    Task CreateImportOrder(int userId, string supplier, List<StockMovementDetail> details);
    
    // Tạo phiếu xuất kho nội bộ
    Task CreateInternalExport(int userId, string note, List<StockMovementDetail> details);
    
    // Duyệt đơn
    Task ApproveMovement(int movementId, int approvedById, Dictionary<int, DateTime>? expiryDates = null);

    // Hủy đơn
    Task CancelMovement(int movementId);

    // Kích hoạt quét tự động tạo đơn nhập
    Task TriggerAutoReorderCheck(string productSku);
}
