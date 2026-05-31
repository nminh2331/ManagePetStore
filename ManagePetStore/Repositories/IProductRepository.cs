using ManagePetStore.Models;

namespace ManagePetStore.Repositories;

public interface IProductRepository
{
    /// <summary>Returns all products with their Category eagerly loaded.</summary>
    Task<IEnumerable<Product>> GetAllWithCategoryAsync();

    /// <summary>Returns a single product by SKU (without navigation properties).</summary>
    Task<Product?> GetBySkuAsync(string sku);

    /// <summary>Returns the total number of distinct product categories.</summary>
    Task<int> GetCategoryCountAsync();

    /// <summary>Adds a new product and persists the change.</summary>
    Task AddAsync(Product product);

    /// <summary>Updates an existing product and persists the change.</summary>
    Task UpdateAsync(Product product);

    /// <summary>Deletes a product by SKU and persists the change.</summary>
    Task DeleteAsync(string sku);

    /// <summary>Returns true if a product with the given SKU exists.</summary>
    Task<bool> ExistsAsync(string sku);
}
