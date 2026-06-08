using System;
using System.Collections.Generic;

namespace ManagePetStore.Models;

public partial class Order
{
    public string OrderId { get; set; } = null!;

    public int CustomerId { get; set; }

    public decimal Subtotal { get; set; }

    public decimal Discount { get; set; }

    public decimal Total { get; set; }

    public string PaymentMethod { get; set; } = null!;

    public int PointsRedeemed { get; set; }

    public int PointsEarned { get; set; }

    public string Status { get; set; } = null!;

    public DateTime Date { get; set; }

    public virtual Customer Customer { get; set; } = null!;

    public virtual ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
}
