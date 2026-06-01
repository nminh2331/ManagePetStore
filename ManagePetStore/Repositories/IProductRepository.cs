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
    /// Returns all products with their Category eagerly loaded.
    Task<IEnumerable<Product>> GetAllWithCategory();

    /// Returns a single product by SKU (without navigation properties).
    Task<Product?> GetProductBySku(string sku);

    /// Returns the total number of distinct product categories.
    Task<int> GetCategoryCount();

    /// Adds a new product and persists the change.
    Task AddProduct(Product product);

    /// Updates an existing product and persists the change.
    Task UpdateProduct(Product product);

    /// Deletes a product by SKU and persists the change.
    Task DeleteProduct(string sku);

    /// Returns true if a product with the given SKU exists.
    Task<bool> ProductExists(string sku);
}
