/**
 * Project: Pet Store Management System (PSMS)
 * File: ProductRepository.cs
 * Author: Tran Duong
 * Date: May 31, 2026
 * Description: Các thao tác CRUD cho bảng sản phẩm.
 */
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

    public async Task<IEnumerable<Product>> GetAllWithCategory()
    {
        return await _context.Products
            .Include(p => p.Category)
            .OrderBy(p => p.Name)
            .ToListAsync();
    }

    public async Task<Product?> GetProductBySku(string sku)
    {
        return await _context.Products.FindAsync(sku);
    }

    public async Task<int> GetCategoryCount()
    {
        return await _context.ProductCategories.CountAsync();
    }

    public async Task AddProduct(Product product)
    {
        _context.Products.Add(product);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateProduct(Product product)
    {
        _context.Products.Update(product);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteProduct(string sku)
    {
        var product = await _context.Products.FindAsync(sku);
        if (product is not null)
        {
            _context.Products.Remove(product);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<bool> ProductExists(string sku)
    {
        return await _context.Products.AnyAsync(p => p.Sku == sku);
    }
}
