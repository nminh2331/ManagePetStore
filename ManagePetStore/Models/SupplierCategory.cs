namespace ManagePetStore.Models;

public partial class SupplierCategory
{
    public int SupplierId { get; set; }
    public int CategoryId { get; set; }

    public virtual Supplier Supplier { get; set; } = null!;
    public virtual ProductCategory Category { get; set; } = null!;
}
