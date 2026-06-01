/**
 * Project: Pet Store Management System (PSMS)
 * File: IProductCategoryRepository.cs
 * Author: Tran Duong
 * Date: May 31, 2026
 * Description: Định nghĩa giao diện truy xuất cơ sở dữ liệu cho bảng danh mục sản phẩm (ProductCategory).
 */
using ManagePetStore.Models;

namespace ManagePetStore.Repositories;

public interface IProductCategoryRepository
{
    /// Returns all categories with their Products eagerly loaded.
    Task<IEnumerable<ProductCategory>> GetAllWithProducts();

    /// Returns all categories without navigation properties (for dropdowns etc.).
    Task<IEnumerable<ProductCategory>> GetAllCategories();

    /// Returns a single category by ID (without navigation properties).
    Task<ProductCategory?> GetCategoryById(int id);

    /// Adds a new category and persists the change.
    Task AddCategory(ProductCategory category);

    /// Updates an existing category and persists the change.
    Task UpdateCategory(ProductCategory category);

    /// Deletes a category by ID and persists the change.
    Task DeleteCategory(int id);

    /// Returns true if a category with the given ID exists.
    Task<bool> CategoryExists(int id);
}
