/**
 * Project: Pet Store Management System (PSMS)
 * File: InventoryBatchRepository.cs
 * Author: Tran Duong
 * Date: June 10, 2026
 * Description: Triển khai các thao tác CRUD cho bảng lô hàng (InventoryBatch).
 */
using ManagePetStore.Models;
using Microsoft.EntityFrameworkCore;

namespace ManagePetStore.Repositories.Warehouse;

public class InventoryBatchRepository : IInventoryBatchRepository
{
    private readonly PetStoreManagementContext _context;

    public InventoryBatchRepository(PetStoreManagementContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<InventoryBatch>> GetBatchesByProductSku(string productSku)
    {
        return await _context.InventoryBatchs
            .Where(b => b.ProductSku == productSku)
            .OrderByDescending(b => b.ReceivedDate)
            .ToListAsync();
    }

    public async Task<InventoryBatch?> GetBatchById(int batchId)
    {
        return await _context.InventoryBatchs.FindAsync(batchId);
    }

    public async Task AddBatch(InventoryBatch batch)
    {
        _context.InventoryBatchs.Add(batch);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateBatch(InventoryBatch batch)
    {
        _context.InventoryBatchs.Update(batch);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteBatch(int batchId)
    {
        var batch = await _context.InventoryBatchs.FindAsync(batchId);
        if (batch is not null)
        {
            _context.InventoryBatchs.Remove(batch);
            await _context.SaveChangesAsync();
        }
    }
}
