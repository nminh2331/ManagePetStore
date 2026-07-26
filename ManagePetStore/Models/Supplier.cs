using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ManagePetStore.Models;

public partial class Supplier
{
    public int SupplierId { get; set; }

    [Required(ErrorMessage = "Tên nhà cung cấp không được để trống.")]
    [StringLength(255, ErrorMessage = "Tên nhà cung cấp không được vượt quá 255 ký tự.")]
    public string Name { get; set; } = null!;

    [Required(ErrorMessage = "Số điện thoại không được để trống.")]
    [RegularExpression(@"^(0[3|5|7|8|9])+([0-9]{8})$", ErrorMessage = "Số điện thoại không hợp lệ (phải bắt đầu bằng 0 và có 10 chữ số).")]
    public string? Phone { get; set; }

    [Required(ErrorMessage = "Email không được để trống.")]
    [EmailAddress(ErrorMessage = "Email không đúng định dạng.")]
    public string? Email { get; set; }

    [Required(ErrorMessage = "Địa chỉ không được để trống.")]
    public string? Address { get; set; }

    public bool IsActive { get; set; }

    public virtual ICollection<StockMovement> StockMovements { get; set; } = new List<StockMovement>();

    public virtual ICollection<ProductCategory> Categories { get; set; } = new List<ProductCategory>();

    /// <summary>Sản phẩm cụ thể NCC này có thể cung cấp (tùy chọn)</summary>
    public virtual ICollection<SupplierProduct> SupplierProducts { get; set; } = new List<SupplierProduct>();
}
