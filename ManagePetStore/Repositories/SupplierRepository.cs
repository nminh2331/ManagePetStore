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
            .Include(s => s.Categories)
            .ToListAsync();
    }

    public async Task<Supplier?> GetSupplierByIdAsync(int id)
    {
        return await _context.Suppliers
            .Include(s => s.Categories)
            .FirstOrDefaultAsync(s => s.SupplierId == id);
    }

    public async Task<IEnumerable<Supplier>> GetSuppliersByCategoryAsync(int categoryId)
    {
        return await _context.Suppliers
            .Where(s => s.IsActive && s.Categories.Any(c => c.CategoryId == categoryId))
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
            // Entity Framework will automatically handle the many-to-many join table deletion
            // when we remove the supplier if cascade delete is configured, or we can clear categories.
            await _context.Entry(supplier).Collection(s => s.Categories).LoadAsync();
            supplier.Categories.Clear();
            
            _context.Suppliers.Remove(supplier);
            await _context.SaveChangesAsync();
        }
    }

    public async Task AssignCategoriesToSupplierAsync(int supplierId, List<int> categoryIds)
    {
        var supplier = await _context.Suppliers.Include(s => s.Categories).FirstOrDefaultAsync(s => s.SupplierId == supplierId);
        if (supplier == null) return;

        supplier.Categories.Clear();

        if (categoryIds != null)
        {
            foreach (var categoryId in categoryIds)
            {
                var category = await _context.ProductCategories.FindAsync(categoryId);
                if (category != null)
                {
                    supplier.Categories.Add(category);
                }
            }
        }

        await _context.SaveChangesAsync();
    }
}
