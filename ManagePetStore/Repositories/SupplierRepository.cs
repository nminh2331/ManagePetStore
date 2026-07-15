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
            // Soft delete: đánh dấu ngừng hoạt động thay vì xóa khỏi DB
            supplier.IsActive = false;
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

    public async Task AssignProductsToSupplierAsync(int supplierId, List<string> productSkus)
    {
        // Xóa các liên kết cũ
        var existing = _context.SupplierProducts.Where(sp => sp.SupplierId == supplierId);
        _context.SupplierProducts.RemoveRange(existing);

        // Thêm các liên kết mới
        if (productSkus != null && productSkus.Any())
        {
            foreach (var sku in productSkus.Distinct())
            {
                _context.SupplierProducts.Add(new SupplierProduct
                {
                    SupplierId = supplierId,
                    ProductSku = sku
                });
            }
        }
        await _context.SaveChangesAsync();
    }

    public async Task<List<string>> GetSupplierProductSkusAsync(int supplierId)
    {
        return await _context.SupplierProducts
            .Where(sp => sp.SupplierId == supplierId)
            .Select(sp => sp.ProductSku)
            .ToListAsync();
    }

    public async Task<IEnumerable<Supplier>> GetSuppliersByProductSkuAsync(string sku)
    {
        return await _context.Suppliers
            .Where(s => s.IsActive && s.SupplierProducts.Any(sp => sp.ProductSku == sku))
            .ToListAsync();
    }
}
