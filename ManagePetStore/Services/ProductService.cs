/**
 * Project: Pet Store Management System (PSMS)
 * File: ProductService.cs
 * Author: Tran Duong
 * Date: May 31, 2026
 * Description: Triển khai các hàm xử lý logic nghiệp vụ cho quản lý sản phẩm và tồn kho của cửa hàng.
 */
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

    // Index summary 

    public async Task<ProductSummaryViewModel> GetProductSummary()
    {
        var products = (await _productRepo.GetAllWithCategory()).ToList();

        return new ProductSummaryViewModel
        {
            Products        = products,
            TotalProducts   = products.Count,
            LowStockCount   = products.Count(p => p.Stock > 0 && p.Stock <= p.MinStock),
            OutOfStockCount = products.Count(p => p.Stock == 0),
            TotalValue      = products.Sum(p => p.Price * p.Stock),
            CategoryCount   = await _productRepo.GetCategoryCount()
        };
    }

    // Get single product 

    public async Task<Product?> GetProductBySku(string sku)
    {
        return await _productRepo.GetProductBySku(sku);
    }

    // SelectList helper 

    public async Task<SelectList> GetCategorySelectList(object? selectedValue = null)
    {
        var categories = await _categoryRepo.GetAllCategories();
        return new SelectList(categories, "CategoryId", "Name", selectedValue);
    }

    // Create

    public async Task CreateProduct(Product product)
    {
        // Validate: SKU must not already exist
        if (await _productRepo.ProductExists(product.Sku.Trim()))
            throw new ServiceException($"Mã sản phẩm '{product.Sku}' đã tồn tại.");

        SanitizeProduct(product);

        await _productRepo.AddProduct(product);
    }

    // Update 

    public async Task UpdateProduct(string routeId, Product product)
    {
        if (routeId != product.Sku)
            throw new ServiceException("Mã sản phẩm không khớp.");

        if (!await _productRepo.ProductExists(product.Sku))
            throw new ServiceException($"Không tìm thấy sản phẩm '{product.Sku}'.");

        SanitizeProduct(product);

        try
        {
            await _productRepo.UpdateProduct(product);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ServiceException("Sản phẩm đã bị thay đổi bởi người khác. Vui lòng tải lại trang.");
        }
    }

    // Delete 

    public async Task DeleteProduct(string sku)
    {
        await _productRepo.DeleteProduct(sku);
    }

    // Private helpers 

    private static void SanitizeProduct(Product p)
    {
        p.Sku           = p.Sku.Trim().ToUpperInvariant();
        p.Name          = p.Name.Trim();
        p.Unit          = p.Unit.Trim();
        p.ShelfLocation = string.IsNullOrWhiteSpace(p.ShelfLocation) ? null : p.ShelfLocation.Trim();
        p.ImageUrl      = string.IsNullOrWhiteSpace(p.ImageUrl)      ? null : p.ImageUrl.Trim();
    }
}
