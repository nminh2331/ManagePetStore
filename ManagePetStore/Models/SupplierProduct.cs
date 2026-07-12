namespace ManagePetStore.Models;

/// <summary>
/// Bảng liên kết Nhà cung cấp ↔ Sản phẩm cụ thể (Phương án A).
/// Tùy chọn: NCC không cần phải đăng ký từng sản phẩm.
/// Nếu có đăng ký, dropdown CreateImport sẽ ưu tiên NCC đó khi chọn sản phẩm này.
/// </summary>
public class SupplierProduct
{
    public int SupplierId { get; set; }
    public string ProductSku { get; set; } = null!;

    public virtual Supplier Supplier { get; set; } = null!;
    public virtual Product Product { get; set; } = null!;
}
