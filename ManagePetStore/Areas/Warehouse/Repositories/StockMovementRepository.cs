/**
 * Project: Pet Store Management System (PSMS)
 * File: StockMovementRepository.cs
 * Author: Tran Duong
 * Date: June 11, 2026
 * Description: Triển khai các thao tác truy xuất dữ liệu cho Phiếu Xuất/Nhập kho.
 */
using ManagePetStore.Models;
using Microsoft.EntityFrameworkCore;

namespace ManagePetStore.Areas.Warehouse.Repositories;

public class StockMovementRepository : IStockMovementRepository
{
    private readonly PetStoreManagementContext _context;

    public StockMovementRepository(PetStoreManagementContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<StockMovement>> GetAllMovements(DateTime? fromDate = null, DateTime? toDate = null)
    {
        var query = _context.StockMovements
            .Include(m => m.CreatedBy)
            .Include(m => m.StockMovementDetails)
                .ThenInclude(d => d.ProductSkuNavigation)
            .AsQueryable();

        if (fromDate.HasValue)
            query = query.Where(m => m.Date >= fromDate.Value);
        
        if (toDate.HasValue)
        {
            var toDateEnd = toDate.Value.Date.AddDays(1).AddTicks(-1);
            query = query.Where(m => m.Date <= toDateEnd);
        }

        return await query.OrderByDescending(m => m.Date).ToListAsync();
    }

    public async Task<StockMovement?> GetMovementById(int id)
    {
        return await _context.StockMovements
            .Include(m => m.CreatedBy)
            .Include(m => m.StockMovementDetails)
                .ThenInclude(d => d.ProductSkuNavigation)
            .FirstOrDefaultAsync(m => m.MovementId == id);
    }

    public async Task AddMovement(StockMovement movement)
    {
        _context.StockMovements.Add(movement);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateMovement(StockMovement movement)
    {
        _context.StockMovements.Update(movement);
        await _context.SaveChangesAsync();
    }

    public async Task<StockMovement?> GetPendingImportByProduct(string productSku)
    {
        // Tìm phiếu nhập đang "Chờ duyệt" mà có chứa sản phẩm này
        return await _context.StockMovements
            .Include(m => m.StockMovementDetails)
            .Where(m => m.Type == "Nhập hàng" && m.Status == "Chờ duyệt")
            .FirstOrDefaultAsync(m => m.StockMovementDetails.Any(d => d.ProductSku == productSku));
    }
}
