/**
 * Project: Pet Store Management System (PSMS)
 * File: ProductCategoryRepository.cs
 * Author: Tran Duong
 * Date: May 31, 2026
 * Description: Các thao tác CRUD cho danh mục sản phẩm.
 */
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

    public async Task<IEnumerable<ProductCategory>> GetAllWithProducts(bool showDeleted = false)
    {
        return await _context.ProductCategories
            .Include(c => c.Products.Where(p => p.IsDeleted == false))
            .Where(c => c.IsDeleted == showDeleted)
            .OrderBy(c => c.Name)
            .ToListAsync();
    }

    public async Task<IEnumerable<ProductCategory>> GetAllCategories(bool showDeleted = false)
    {
        return await _context.ProductCategories
            .Where(c => c.IsDeleted == showDeleted)
            .OrderBy(c => c.Name)
            .ToListAsync();
    }

    public async Task<ProductCategory?> GetCategoryById(int id)
    {
        return await _context.ProductCategories.FindAsync(id);
    }

    public async Task AddCategory(ProductCategory category)
    {
        _context.ProductCategories.Add(category);
        await _context.SaveChangesAsync();
    }

    /// <inheritdoc/>
    public async Task UpdateCategory(ProductCategory category)
    {
        _context.ProductCategories.Update(category);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteCategory(int id)
    {
        var category = await _context.ProductCategories.FindAsync(id);
        if (category is not null)
        {
            category.IsDeleted = true;
            _context.ProductCategories.Update(category);
            await _context.SaveChangesAsync();
        }
    }

    public async Task RestoreCategory(int id)
    {
        var category = await _context.ProductCategories.FindAsync(id);
        if (category is not null)
        {
            category.IsDeleted = false;
            _context.ProductCategories.Update(category);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<bool> CategoryExists(int id)
    {
        return await _context.ProductCategories.AnyAsync(c => c.CategoryId == id);
    }
}
