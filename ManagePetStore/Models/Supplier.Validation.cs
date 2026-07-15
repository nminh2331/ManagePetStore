/**
 * Project: Pet Store Management System (PSMS)
 * File: Supplier.Validation.cs
 * Description: Data annotation validation cho Supplier model (partial class).
 *              Tách riêng để tránh bị mất khi scaffold lại EF entity.
 */
using System.ComponentModel.DataAnnotations;

namespace ManagePetStore.Models;

[MetadataType(typeof(SupplierMetadata))]
public partial class Supplier { }

public class SupplierMetadata
{
    [Required(ErrorMessage = "Tên nhà cung cấp không được để trống.")]
    [StringLength(200, MinimumLength = 2, ErrorMessage = "Tên nhà cung cấp từ 2 đến 200 ký tự.")]
    public string Name { get; set; } = null!;

    [RegularExpression(@"^0\d{9,10}$", ErrorMessage = "Số điện thoại phải bắt đầu bằng 0 và gồm 10-11 chữ số.")]
    [StringLength(20, ErrorMessage = "Số điện thoại tối đa 20 ký tự.")]
    public string? Phone { get; set; }

    [EmailAddress(ErrorMessage = "Email không đúng định dạng.")]
    [StringLength(200, ErrorMessage = "Email tối đa 200 ký tự.")]
    public string? Email { get; set; }

    [StringLength(300, ErrorMessage = "Địa chỉ tối đa 300 ký tự.")]
    public string? Address { get; set; }
}
