/**
 * Project: Pet Store Management System (PSMS)
 * File: IProductRepository.cs
 * Author: Tran Duong
 * Date: May 31, 2026
 * Description: Định nghĩa giao diện truy xuất cơ sở dữ liệu cho bảng sản phẩm (Product) trong kho.
 */
using ManagePetStore.Models;

namespace ManagePetStore.Repositories;

public interface IProductRepository
{
    /// Returns products with their Category eagerly loaded.
    /// filter: "active" (default) = only active, "deleted" = only deleted, "all" = both.
    Task<IEnumerable<Product>> GetAllWithCategory(string filter = "active");

    /// Returns a single product by SKU (without navigation properties).
    Task<Product?> GetProductBySku(string sku);

    /// Returns the total number of distinct active product categories.
    Task<int> GetCategoryCount();

    /// Adds a new product and persists the change.
    Task AddProduct(Product product);

    /// Updates an existing product and persists the change.
    Task UpdateProduct(Product product);

    /// Soft deletes a product by SKU (sets IsDeleted = true).
    Task DeleteProduct(string sku);

    /// Restores a soft-deleted product by SKU (sets IsDeleted = false).
    Task RestoreProduct(string sku);

    /// Returns true if a product with the given SKU exists.
    Task AdjustStockAsync(string sku, int amount);
    Task<bool> ProductExists(string sku);
}
