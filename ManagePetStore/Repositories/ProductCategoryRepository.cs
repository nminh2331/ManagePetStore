using ManagePetStore.Models;
using Microsoft.EntityFrameworkCore;

namespace ManagePetStore.Repositories;

public class ProductCategoryRepository : IProductCategoryRepository
{
    private readonly PetStoreManagementContext _context;

    public ProductCategoryRepository(PetStoreManagementContext context)
    {
        _context = context;
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<ProductCategory>> GetAllWithProductsAsync()
    {
        return await _context.ProductCategories
            .Include(c => c.Products)
            .OrderBy(c => c.Name)
            .ToListAsync();
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<ProductCategory>> GetAllAsync()
    {
        return await _context.ProductCategories
            .OrderBy(c => c.Name)
            .ToListAsync();
    }

    /// <inheritdoc/>
    public async Task<ProductCategory?> GetByIdAsync(int id)
    {
        return await _context.ProductCategories.FindAsync(id);
    }

    /// <inheritdoc/>
    public async Task AddAsync(ProductCategory category)
    {
        _context.ProductCategories.Add(category);
        await _context.SaveChangesAsync();
    }

    /// <inheritdoc/>
    public async Task UpdateAsync(ProductCategory category)
    {
        _context.ProductCategories.Update(category);
        await _context.SaveChangesAsync();
    }

    /// <inheritdoc/>
    public async Task DeleteAsync(int id)
    {
        var category = await _context.ProductCategories.FindAsync(id);
        if (category is not null)
        {
            _context.ProductCategories.Remove(category);
            await _context.SaveChangesAsync();
        }
    }

    /// <inheritdoc/>
    public async Task<bool> ExistsAsync(int id)
    {
        return await _context.ProductCategories.AnyAsync(c => c.CategoryId == id);
    }
}
