/**
 * Project: Pet Store Management System (PSMS)
 * File: IProductService.cs
 * Author: Tran Duong
 * Date: May 31, 2026
 * Description: Định nghĩa giao diện cho dịch vụ quản lý sản phẩm và tính toán thống kê tồn kho hàng hóa.
 */
using ManagePetStore.Models;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace ManagePetStore.Services;

public interface IProductService
{
    /// Returns the full product list together with pre-calculated warehouse stats.
    Task<ProductSummaryViewModel> GetProductSummary(string? search = null, string filter = "active");

    /// Returns a single product by SKU, or null if not found.
    Task<Product?> GetProductBySku(string sku);

    /// Returns a SelectList of all categories, optionally pre-selecting one.
    Task<SelectList> GetCategorySelectList(object? selectedValue = null);

    /// Validates and creates a new product.
    /// Throws <see cref="ManagePetStore.Exceptions.ServiceException"/> on business-rule violations.
    Task CreateProduct(Product product);

    /// Validates and updates an existing product.
    /// Throws <see cref="ManagePetStore.Exceptions.ServiceException"/> if not found or SKU mismatch.
    Task UpdateProduct(string routeId, Product product);

    /// Deletes a product by SKU. No-op if not found.
    Task DeleteProduct(string sku);

    /// Restores a soft-deleted product by SKU.
    Task RestoreProduct(string sku);
}
