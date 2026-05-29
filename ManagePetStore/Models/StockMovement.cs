using System;
using System.Collections.Generic;

namespace ManagePetStore.Models;

public partial class StockMovement
{
    public int MovementId { get; set; }

    public string Type { get; set; } = null!;

    public int CreatedById { get; set; }

    public string? Supplier { get; set; }

    public decimal TotalValue { get; set; }

    public DateTime Date { get; set; }

    public string Status { get; set; } = null!;

    public virtual User CreatedBy { get; set; } = null!;

    public virtual ICollection<StockMovementDetail> StockMovementDetails { get; set; } = new List<StockMovementDetail>();
}
