using ManagePetStore.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ManagePetStore.Repositories;

public class SupplierRepository : ISupplierRepository
{
    private readonly PetStoreManagementContext _context;

    public SupplierRepository(PetStoreManagementContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<Supplier>> GetAllSuppliersAsync()
    {
        return await _context.Suppliers
            .Include(s => s.SupplierCategories)
                .ThenInclude(sc => sc.Category)
            .ToListAsync();
    }

    public async Task<Supplier?> GetSupplierByIdAsync(int id)
    {
        return await _context.Suppliers
            .Include(s => s.SupplierCategories)
            .FirstOrDefaultAsync(s => s.SupplierId == id);
    }

    public async Task<IEnumerable<Supplier>> GetSuppliersByCategoryAsync(int categoryId)
    {
        return await _context.Suppliers
            .Where(s => s.IsActive && s.SupplierCategories.Any(sc => sc.CategoryId == categoryId))
            .ToListAsync();
    }

    public async Task AddSupplierAsync(Supplier supplier)
    {
        _context.Suppliers.Add(supplier);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateSupplierAsync(Supplier supplier)
    {
        _context.Suppliers.Update(supplier);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteSupplierAsync(int id)
    {
        var supplier = await _context.Suppliers.FindAsync(id);
        if (supplier != null)
        {
            // Instead of hard delete, maybe soft delete? Or hard delete if no relations.
            // Let's do hard delete for now, but handle related SupplierCategories
            var relatedCategories = _context.SupplierCategories.Where(sc => sc.SupplierId == id);
            _context.SupplierCategories.RemoveRange(relatedCategories);
            
            _context.Suppliers.Remove(supplier);
            await _context.SaveChangesAsync();
        }
    }

    public async Task AssignCategoriesToSupplierAsync(int supplierId, List<int> categoryIds)
    {
        var existingLinks = await _context.SupplierCategories
            .Where(sc => sc.SupplierId == supplierId)
            .ToListAsync();
            
        _context.SupplierCategories.RemoveRange(existingLinks);

        if (categoryIds != null && categoryIds.Any())
        {
            var newLinks = categoryIds.Select(cId => new SupplierCategory
            {
                SupplierId = supplierId,
                CategoryId = cId
            });
            _context.SupplierCategories.AddRange(newLinks);
        }

        await _context.SaveChangesAsync();
    }
}
