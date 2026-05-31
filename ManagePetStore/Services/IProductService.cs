using ManagePetStore.Models;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace ManagePetStore.Services;

public interface IProductService
{
    /// <summary>
    /// Returns the full product list together with pre-calculated warehouse stats.
    /// </summary>
    Task<ProductSummaryViewModel> GetSummaryAsync();

    /// <summary>
    /// Returns a single product by SKU, or null if not found.
    /// </summary>
    Task<Product?> GetBySkuAsync(string sku);

    /// <summary>
    /// Returns a SelectList of all categories, optionally pre-selecting one.
    /// </summary>
    Task<SelectList> GetCategorySelectListAsync(object? selectedValue = null);

    /// <summary>
    /// Validates and creates a new product.
    /// Throws <see cref="ManagePetStore.Exceptions.ServiceException"/> on business-rule violations.
    /// </summary>
    Task CreateAsync(Product product);

    /// <summary>
    /// Validates and updates an existing product.
    /// Throws <see cref="ManagePetStore.Exceptions.ServiceException"/> if not found or SKU mismatch.
    /// </summary>
    Task UpdateAsync(string routeId, Product product);

    /// <summary>
    /// Deletes a product by SKU. No-op if not found.
    /// </summary>
    Task DeleteAsync(string sku);
}
