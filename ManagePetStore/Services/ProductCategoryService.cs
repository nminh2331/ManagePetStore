/**
 * Project: Pet Store Management System (PSMS)
 * File: ProductCategoryService.cs
 * Author: Tran Duong
 * Date: May 31, 2026
 * Description: C�c h�m x? l� logic nghi?p v? cho vi?c qu?n l� danh m?c s?n ph?m.
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

    public async Task<CategorySummaryViewModel> GetCategorySummary(bool showDeleted = false)
    {
        var categories = (await _categoryRepo.GetAllWithProducts(showDeleted)).ToList();

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
            throw new ServiceException("ID danh m?c kh�ng kh?p.");

        if (!await _categoryRepo.CategoryExists(category.CategoryId))
            throw new ServiceException($"Kh�ng t�m th?y danh m?c ID = {category.CategoryId}.");

        SanitizeCategory(category);

        try
        {
            await _categoryRepo.UpdateCategory(category);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ServiceException("Danh m?c d� b? thay d?i b?i ngu?i kh�c. Vui l�ng t?i l?i trang.");
        }
    }

    // Delete 

    public async Task DeleteCategory(int id)
    {
        await _categoryRepo.DeleteCategory(id);
    }

    public async Task RestoreCategory(int id)
    {
        await _categoryRepo.RestoreCategory(id);
    }

    // Private helpers 

    private static void SanitizeCategory(ProductCategory c)
    {
        c.Name        = c.Name.Trim();
        c.Description = string.IsNullOrWhiteSpace(c.Description) ? null : c.Description.Trim();
    }
}
