using ManagePetStore.Models;

namespace ManagePetStore.Services;

public interface IProductCategoryService
{
    /// <summary>
    /// Returns the full category list together with pre-calculated stats.
    /// </summary>
    Task<CategorySummaryViewModel> GetSummaryAsync();

    /// <summary>
    /// Returns a single category by ID, or null if not found.
    /// </summary>
    Task<ProductCategory?> GetByIdAsync(int id);

    /// <summary>
    /// Validates and creates a new category.
    /// Throws <see cref="ManagePetStore.Exceptions.ServiceException"/> on business-rule violations.
    /// </summary>
    Task CreateAsync(ProductCategory category);

    /// <summary>
    /// Validates and updates an existing category.
    /// Throws <see cref="ManagePetStore.Exceptions.ServiceException"/> if not found or ID mismatch.
    /// </summary>
    Task UpdateAsync(int routeId, ProductCategory category);

    /// <summary>
    /// Deletes a category by ID. No-op if not found.
    /// </summary>
    Task DeleteAsync(int id);
}
