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
    /// <summary>
    /// Returns the full product list together with pre-calculated warehouse stats.
    /// </summary>
    Task<ProductSummaryViewModel> GetProductSummary();

    /// <summary>
    /// Returns a single product by SKU, or null if not found.
    /// </summary>
    Task<Product?> GetProductBySku(string sku);

    /// <summary>
    /// Returns a SelectList of all categories, optionally pre-selecting one.
    /// </summary>
    Task<SelectList> GetCategorySelectList(object? selectedValue = null);

    /// <summary>
    /// Validates and creates a new product.
    /// Throws <see cref="ManagePetStore.Exceptions.ServiceException"/> on business-rule violations.
    /// </summary>
    Task CreateProduct(Product product);

    /// <summary>
    /// Validates and updates an existing product.
    /// Throws <see cref="ManagePetStore.Exceptions.ServiceException"/> if not found or SKU mismatch.
    /// </summary>
    Task UpdateProduct(string routeId, Product product);

    /// <summary>
    /// Deletes a product by SKU. No-op if not found.
    /// </summary>
    Task DeleteProduct(string sku);
}
