/**
 * Project: Pet Store Management System (PSMS)
 * File: IProductCategoryService.cs
 * Author: Tran Duong
 * Date: May 31, 2026
 * Description: Định nghĩa giao diện cho dịch vụ quản lý danh mục sản phẩm (CRUD và thống kê danh mục).
 */
using ManagePetStore.Models;

namespace ManagePetStore.Services;

public interface IProductCategoryService
{
    /// Returns the full category list together with pre-calculated stats.
    Task<CategorySummaryViewModel> GetCategorySummary(bool showDeleted = false);

    /// Returns a single category by ID, or null if not found.
    Task<ProductCategory?> GetCategoryById(int id);

    /// Validates and creates a new category.
    Task CreateCategory(ProductCategory category);

    /// Validates and updates an existing category.
    Task UpdateCategory(int routeId, ProductCategory category);

    /// Deletes a category by ID. No-op if not found.
    Task DeleteCategory(int id);

    /// Restores a soft-deleted category by ID.
    Task RestoreCategory(int id);
}
