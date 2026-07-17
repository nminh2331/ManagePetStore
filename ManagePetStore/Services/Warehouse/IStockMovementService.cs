/**
 * Project: Pet Store Management System (PSMS)
 * File: IStockMovementService.cs
 * Author: Tran Duong
 * Date: June 11, 2026
 * Last Update: July 17, 2026
 * Description: Giao diá»‡n dá»‹ch vá»¥ xá»­ lÃ½ nghiá»‡p vá»¥ Nháº­p/Xuáº¥t kho.
 */
using ManagePetStore.Models;

namespace ManagePetStore.Services.Warehouse;

public interface IStockMovementService
{
    Task<IEnumerable<StockMovement>> GetAllMovements(DateTime? fromDate = null, DateTime? toDate = null, string? search = null);
    Task<StockMovement?> GetMovementById(int id);
    
    // Táº¡o Ä‘Æ¡n nháº­p hÃ ng (Purchase Order)
    Task CreateImportOrder(int userId, int? supplierId, List<StockMovementDetail> details);
    
    // Táº¡o phiáº¿u xuáº¥t kho ná»™i bá»™
    Task CreateInternalExport(int userId, string note, List<StockMovementDetail> details);
    
    // Duyá»‡t Ä‘Æ¡n
    Task ApproveMovement(int movementId, int approvedById, Dictionary<int, DateTime>? expiryDates = null);

    // Tá»± Ä‘á»™ng táº¡o phiáº¿u xuáº¥t/nháº­p khi cÃ³ thay Ä‘á»•i tá»« Ä‘Æ¡n hÃ ng (Sales, Return)
    Task CreateSystemMovement(int systemUserId, string type, string status, string? supplier, decimal totalValue, List<StockMovementDetail> details);

    // Há»§y Ä‘Æ¡n
    Task CancelMovement(int movementId);

    // KÃ­ch hoáº¡t quÃ©t tá»± Ä‘á»™ng táº¡o Ä‘Æ¡n nháº­p
    Task TriggerAutoReorderCheck(string productSku);
}
