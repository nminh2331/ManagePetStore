/**
 * Project: Pet Store Management System (PSMS)
 * File: ProductCategoryService.cs
 * Author: Tran Duong
 * Date: May 31, 2026
 * Description: Các hàm xử lý logic nghiệp vụ cho việc quản lý danh mục sản phẩm.
 */
using ManagePetStore.Exceptions;
using ManagePetStore.Models;
using ManagePetStore.Repositories;
using Microsoft.EntityFrameworkCore;

namespace ManagePetStore.Services;

public class ProductCategoryService : IProductCategoryService
{
    private readonly IProductCategoryRepository _categoryRepo;

    public ProductCategoryService(IProductCategoryRepository categoryRepo)
    {
        _categoryRepo = categoryRepo;
    }

    // Index summary 

    public async Task<CategorySummaryViewModel> GetCategorySummary()
    {
        var categories = (await _categoryRepo.GetAllWithProducts()).ToList();

        return new CategorySummaryViewModel
        {
            Categories      = categories,
            TotalCategories = categories.Count,
            TotalProducts   = categories.Sum(c => c.Products.Count),
            EmptyCategories = categories.Count(c => c.Products.Count == 0)
        };
    }

    // Get single category 

    public async Task<ProductCategory?> GetCategoryById(int id)
    {
        return await _categoryRepo.GetCategoryById(id);
    }

    // Create 

    public async Task CreateCategory(ProductCategory category)
    {
        SanitizeCategory(category);
        await _categoryRepo.AddCategory(category);
    }

    // Update 

    public async Task UpdateCategory(int routeId, ProductCategory category)
    {
        if (routeId != category.CategoryId)
            throw new ServiceException("ID danh mục không khớp.");

        if (!await _categoryRepo.CategoryExists(category.CategoryId))
            throw new ServiceException($"Không tìm thấy danh mục ID = {category.CategoryId}.");

        SanitizeCategory(category);

        try
        {
            await _categoryRepo.UpdateCategory(category);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ServiceException("Danh mục đã bị thay đổi bởi người khác. Vui lòng tải lại trang.");
        }
    }

    // Delete 

    public async Task DeleteCategory(int id)
    {
        await _categoryRepo.DeleteCategory(id);
    }

    // Private helpers 

    private static void SanitizeCategory(ProductCategory c)
    {
        c.Name        = c.Name.Trim();
        c.Description = string.IsNullOrWhiteSpace(c.Description) ? null : c.Description.Trim();
    }
}
