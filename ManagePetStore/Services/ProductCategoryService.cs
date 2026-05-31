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

    // ─── Index summary ────────────────────────────────────────────────────────

    public async Task<CategorySummaryViewModel> GetSummaryAsync()
    {
        var categories = (await _categoryRepo.GetAllWithProductsAsync()).ToList();

        return new CategorySummaryViewModel
        {
            Categories      = categories,
            TotalCategories = categories.Count,
            TotalProducts   = categories.Sum(c => c.Products.Count),
            EmptyCategories = categories.Count(c => c.Products.Count == 0)
        };
    }

    // ─── Single category ──────────────────────────────────────────────────────

    public async Task<ProductCategory?> GetByIdAsync(int id)
    {
        return await _categoryRepo.GetByIdAsync(id);
    }

    // ─── Create ───────────────────────────────────────────────────────────────

    public async Task CreateAsync(ProductCategory category)
    {
        SanitizeCategory(category);
        await _categoryRepo.AddAsync(category);
    }

    // ─── Update ───────────────────────────────────────────────────────────────

    public async Task UpdateAsync(int routeId, ProductCategory category)
    {
        if (routeId != category.CategoryId)
            throw new ServiceException("ID danh mục không khớp.");

        if (!await _categoryRepo.ExistsAsync(category.CategoryId))
            throw new ServiceException($"Không tìm thấy danh mục ID = {category.CategoryId}.");

        SanitizeCategory(category);

        try
        {
            await _categoryRepo.UpdateAsync(category);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ServiceException("Danh mục đã bị thay đổi bởi người khác. Vui lòng tải lại trang.");
        }
    }

    // ─── Delete ───────────────────────────────────────────────────────────────

    public async Task DeleteAsync(int id)
    {
        await _categoryRepo.DeleteAsync(id);
    }

    // ─── Private helpers ──────────────────────────────────────────────────────

    private static void SanitizeCategory(ProductCategory c)
    {
        c.Name        = c.Name.Trim();
        c.Description = string.IsNullOrWhiteSpace(c.Description) ? null : c.Description.Trim();
    }
}
