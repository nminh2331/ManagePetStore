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

    public int? OrderStatus { get; set; }

    public string? CancelReason { get; set; }

    public string? CanceledBy { get; set; }

    public DateTime? CanceledAt { get; set; }

    public virtual Customer Customer { get; set; } = null!;

    public virtual ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();

    public virtual ICollection<OrderTracking> OrderTrackings { get; set; } = new List<OrderTracking>();

    public virtual ICollection<ReturnRequest> ReturnRequests { get; set; } = new List<ReturnRequest>();

    public virtual ICollection<WalletTransaction> WalletTransactions { get; set; } = new List<WalletTransaction>();
}
