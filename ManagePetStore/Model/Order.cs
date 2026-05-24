using System;
using System.Collections.Generic;

namespace ManagePetStore.Model;

public partial class Order
{
    public int OrderId { get; set; }

    public int? CustomerId { get; set; }

    public int? UserId { get; set; }

    public string OrderType { get; set; } = null!;

    public decimal TotalAmount { get; set; }

    public decimal? VoucherDiscount { get; set; }

    public decimal? PointsDiscount { get; set; }

    public decimal FinalAmount { get; set; }

    public string? OrderStatus { get; set; }

    public string? ShippingAddress { get; set; }

    public string? Phone { get; set; }

    public string? Notes { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual Customer? Customer { get; set; }

    public virtual ICollection<OrderDetail> OrderDetails { get; set; } = new List<OrderDetail>();

    public virtual ICollection<Payment> Payments { get; set; } = new List<Payment>();

    public virtual User? User { get; set; }
}
