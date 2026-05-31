using ManagePetStore.Models;

namespace ManagePetStore.Repositories;

public interface IProductCategoryRepository
{
    /// <summary>Returns all categories with their Products eagerly loaded.</summary>
    Task<IEnumerable<ProductCategory>> GetAllWithProductsAsync();

    /// <summary>Returns all categories without navigation properties (for dropdowns etc.).</summary>
    Task<IEnumerable<ProductCategory>> GetAllAsync();

    /// <summary>Returns a single category by ID (without navigation properties).</summary>
    Task<ProductCategory?> GetByIdAsync(int id);

    /// <summary>Adds a new category and persists the change.</summary>
    Task AddAsync(ProductCategory category);

    /// <summary>Updates an existing category and persists the change.</summary>
    Task UpdateAsync(ProductCategory category);

    /// <summary>Deletes a category by ID and persists the change.</summary>
    Task DeleteAsync(int id);

    /// <summary>Returns true if a category with the given ID exists.</summary>
    Task<bool> ExistsAsync(int id);
}
