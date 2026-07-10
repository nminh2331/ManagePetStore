using System;
using System.Collections.Generic;

namespace ManagePetStore.Models;

public partial class ReturnRequest
{
    public int RequestId { get; set; }

    public string OrderId { get; set; } = null!;

    public int CustomerId { get; set; }

    public string Reason { get; set; } = null!;

    public string Status { get; set; } = null!;

    public decimal RefundAmount { get; set; }

    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; }

    public int? ProcessedBy { get; set; }

    public DateTime? ProcessedAt { get; set; }

    public virtual Customer Customer { get; set; } = null!;

    public virtual Order Order { get; set; } = null!;

    public virtual User? ProcessedByNavigation { get; set; }

    public virtual ICollection<ReturnRequestItem> ReturnRequestItems { get; set; } = new List<ReturnRequestItem>();
}
