using System;
using System.Collections.Generic;

namespace ManagePetStore.Models;

public partial class Product
{
    public string Sku { get; set; } = null!;

    public string Name { get; set; } = null!;

    public string Category { get; set; } = null!;

    public string Unit { get; set; } = null!;

    public int Stock { get; set; }

    public int MinStock { get; set; }

    public DateTime? ExpiryDate { get; set; }

    public string? ShelfLocation { get; set; }

    public decimal Price { get; set; }

    public string? ImageUrl { get; set; }

    public virtual ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();

    public virtual ICollection<StockMovementDetail> StockMovementDetails { get; set; } = new List<StockMovementDetail>();
}
