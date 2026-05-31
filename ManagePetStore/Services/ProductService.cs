using ManagePetStore.Exceptions;
using ManagePetStore.Models;
using ManagePetStore.Repositories;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace ManagePetStore.Services;

public class ProductService : IProductService
{
    private readonly IProductRepository _productRepo;
    private readonly IProductCategoryRepository _categoryRepo;

    public ProductService(
        IProductRepository productRepo,
        IProductCategoryRepository categoryRepo)
    {
        _productRepo = productRepo;
        _categoryRepo = categoryRepo;
    }

    // ─── Index summary ────────────────────────────────────────────────────────

    public async Task<ProductSummaryViewModel> GetSummaryAsync()
    {
        var products = (await _productRepo.GetAllWithCategoryAsync()).ToList();

        return new ProductSummaryViewModel
        {
            Products        = products,
            TotalProducts   = products.Count,
            LowStockCount   = products.Count(p => p.Stock > 0 && p.Stock <= p.MinStock),
            OutOfStockCount = products.Count(p => p.Stock == 0),
            TotalValue      = products.Sum(p => p.Price * p.Stock),
            CategoryCount   = await _productRepo.GetCategoryCountAsync()
        };
    }

    // ─── Single product ───────────────────────────────────────────────────────

    public async Task<Product?> GetBySkuAsync(string sku)
    {
        return await _productRepo.GetBySkuAsync(sku);
    }

    // ─── SelectList helper ────────────────────────────────────────────────────

    public async Task<SelectList> GetCategorySelectListAsync(object? selectedValue = null)
    {
        var categories = await _categoryRepo.GetAllAsync();
        return new SelectList(categories, "CategoryId", "Name", selectedValue);
    }

    // ─── Create ───────────────────────────────────────────────────────────────

    public async Task CreateAsync(Product product)
    {
        // Validate: SKU must not already exist
        if (await _productRepo.ExistsAsync(product.Sku.Trim()))
            throw new ServiceException($"Mã sản phẩm '{product.Sku}' đã tồn tại.");

        SanitizeProduct(product);

        await _productRepo.AddAsync(product);
    }

    // ─── Update ───────────────────────────────────────────────────────────────

    public async Task UpdateAsync(string routeId, Product product)
    {
        if (routeId != product.Sku)
            throw new ServiceException("Mã sản phẩm không khớp.");

        if (!await _productRepo.ExistsAsync(product.Sku))
            throw new ServiceException($"Không tìm thấy sản phẩm '{product.Sku}'.");

        SanitizeProduct(product);

        try
        {
            await _productRepo.UpdateAsync(product);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ServiceException("Sản phẩm đã bị thay đổi bởi người khác. Vui lòng tải lại trang.");
        }
    }

    // ─── Delete ───────────────────────────────────────────────────────────────

    public async Task DeleteAsync(string sku)
    {
        await _productRepo.DeleteAsync(sku);
    }

    // ─── Private helpers ──────────────────────────────────────────────────────

    private static void SanitizeProduct(Product p)
    {
        p.Sku           = p.Sku.Trim().ToUpperInvariant();
        p.Name          = p.Name.Trim();
        p.Unit          = p.Unit.Trim();
        p.ShelfLocation = string.IsNullOrWhiteSpace(p.ShelfLocation) ? null : p.ShelfLocation.Trim();
        p.ImageUrl      = string.IsNullOrWhiteSpace(p.ImageUrl)      ? null : p.ImageUrl.Trim();
    }
}
