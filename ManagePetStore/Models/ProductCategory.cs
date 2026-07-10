using System;
using System.Collections.Generic;

namespace ManagePetStore.Models;

public partial class ProductCategory
{
    public int CategoryId { get; set; }

    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public bool IsDeleted { get; set; }

    public virtual ICollection<Product> Products { get; set; } = new List<Product>();

    public virtual ICollection<Supplier> Suppliers { get; set; } = new List<Supplier>();
}
