/**
 * Project: Pet Store Management System (PSMS)
 * File: Product.Validation.cs
 * Description: Data annotation validation cho Product model (partial class).
 *              Tách riêng để tránh bị mất khi scaffold lại EF entity.
 */
using System.ComponentModel.DataAnnotations;

namespace ManagePetStore.Models;

[MetadataType(typeof(ProductMetadata))]
public partial class Product { }

public class ProductMetadata
{
    [Required(ErrorMessage = "Mã SKU không được để trống.")]
    [StringLength(50, ErrorMessage = "Mã SKU tối đa 50 ký tự.")]
    [RegularExpression(@"^[a-zA-Z0-9\-_]+$", ErrorMessage = "SKU chỉ gồm chữ, số, dấu - và _.")]
    public string Sku { get; set; } = null!;

    [Required(ErrorMessage = "Tên sản phẩm không được để trống.")]
    [StringLength(200, MinimumLength = 2, ErrorMessage = "Tên sản phẩm từ 2 đến 200 ký tự.")]
    public string Name { get; set; } = null!;

    [Required(ErrorMessage = "Đơn vị tính không được để trống.")]
    [StringLength(50, ErrorMessage = "Đơn vị tính tối đa 50 ký tự.")]
    public string Unit { get; set; } = null!;

    [Range(0, int.MaxValue, ErrorMessage = "Số lượng tồn kho phải >= 0.")]
    public int Stock { get; set; }

    [Range(0, int.MaxValue, ErrorMessage = "Tồn kho tối thiểu phải >= 0.")]
    public int MinStock { get; set; }

    [Range(0.01, (double)decimal.MaxValue, ErrorMessage = "Giá bán phải lớn hơn 0.")]
    public decimal Price { get; set; }

    [Range(0, (double)decimal.MaxValue, ErrorMessage = "Giá vốn phải >= 0.")]
    public decimal CostPrice { get; set; }

    [StringLength(100, ErrorMessage = "Vị trí kệ tối đa 100 ký tự.")]
    public string? ShelfLocation { get; set; }

    [StringLength(500, ErrorMessage = "Mô tả tối đa 500 ký tự.")]
    public string? Description { get; set; }
}
