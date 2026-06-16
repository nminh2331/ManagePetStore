using System;
using System.Collections.Generic;

namespace ManagePetStore.Models;

public partial class OrderTracking
{
    public int TrackingId { get; set; }

    public string OrderId { get; set; } = null!;

    public int? OldStatus { get; set; }

    public int NewStatus { get; set; }

    public string? ChangedBy { get; set; }

    public string? Note { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual Order Order { get; set; } = null!;
}
