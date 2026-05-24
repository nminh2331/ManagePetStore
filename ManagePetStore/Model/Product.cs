using System;
using System.Collections.Generic;

namespace ManagePetStore.Model;

public partial class Product
{
    public int ProductId { get; set; }

    public string? Barcode { get; set; }

    public string ProductName { get; set; } = null!;

    public int CategoryId { get; set; }

    public decimal Price { get; set; }

    public decimal CostPrice { get; set; }

    public int? StockQuantity { get; set; }

    public int? MinStockThreshold { get; set; }

    public DateOnly? ExpirationDate { get; set; }

    public string? Description { get; set; }

    public string? ImageUrl { get; set; }

    public bool? IsActive { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual ICollection<CartItem> CartItems { get; set; } = new List<CartItem>();

    public virtual Category Category { get; set; } = null!;

    public virtual ICollection<InternalConsumption> InternalConsumptions { get; set; } = new List<InternalConsumption>();

    public virtual ICollection<InventoryRecordDetail> InventoryRecordDetails { get; set; } = new List<InventoryRecordDetail>();

    public virtual ICollection<OrderDetail> OrderDetails { get; set; } = new List<OrderDetail>();
}
