/**
 * Project: Pet Store Management System (PSMS)
 * File: ProductCategory.Validation.cs
 * Description: Data annotation validation cho ProductCategory model (partial class).
 *              Tách riêng để tránh bị mất khi scaffold lại EF entity.
 */
using System.ComponentModel.DataAnnotations;

namespace ManagePetStore.Models;

[MetadataType(typeof(ProductCategoryMetadata))]
public partial class ProductCategory { }

public class ProductCategoryMetadata
{
    [Required(ErrorMessage = "Tên danh mục không được để trống.")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Tên danh mục từ 2 đến 100 ký tự.")]
    public string Name { get; set; } = null!;

    [StringLength(500, ErrorMessage = "Mô tả tối đa 500 ký tự.")]
    public string? Description { get; set; }

    [StringLength(500, ErrorMessage = "Từ khóa tối đa 500 ký tự.")]
    public string? Keywords { get; set; }
}
