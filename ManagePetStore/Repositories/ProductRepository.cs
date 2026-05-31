using ManagePetStore.Models;
using Microsoft.EntityFrameworkCore;

namespace ManagePetStore.Repositories;

public class ProductRepository : IProductRepository
{
    private readonly PetStoreManagementContext _context;

    public ProductRepository(PetStoreManagementContext context)
    {
        _context = context;
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<Product>> GetAllWithCategoryAsync()
    {
        return await _context.Products
            .Include(p => p.Category)
            .OrderBy(p => p.Name)
            .ToListAsync();
    }

    /// <inheritdoc/>
    public async Task<Product?> GetBySkuAsync(string sku)
    {
        return await _context.Products.FindAsync(sku);
    }

    /// <inheritdoc/>
    public async Task<int> GetCategoryCountAsync()
    {
        return await _context.ProductCategories.CountAsync();
    }

    /// <inheritdoc/>
    public async Task AddAsync(Product product)
    {
        _context.Products.Add(product);
        await _context.SaveChangesAsync();
    }

    /// <inheritdoc/>
    public async Task UpdateAsync(Product product)
    {
        _context.Products.Update(product);
        await _context.SaveChangesAsync();
    }

    /// <inheritdoc/>
    public async Task DeleteAsync(string sku)
    {
        var product = await _context.Products.FindAsync(sku);
        if (product is not null)
        {
            _context.Products.Remove(product);
            await _context.SaveChangesAsync();
        }
    }

    /// <inheritdoc/>
    public async Task<bool> ExistsAsync(string sku)
    {
        return await _context.Products.AnyAsync(p => p.Sku == sku);
    }
}
