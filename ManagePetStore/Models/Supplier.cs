using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ManagePetStore.Models;

public partial class Supplier
{
    public int SupplierId { get; set; }
    
    [Required(ErrorMessage = "Tên nhà cung cấp không được để trống")]
    public string Name { get; set; } = null!;
    
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public bool IsActive { get; set; } = true;

    public virtual ICollection<SupplierCategory> SupplierCategories { get; set; } = new List<SupplierCategory>();
    public virtual ICollection<StockMovement> StockMovements { get; set; } = new List<StockMovement>();
}
