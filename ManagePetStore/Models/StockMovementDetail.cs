using System;
using System.Collections.Generic;

namespace ManagePetStore.Models;

public partial class StockMovementDetail
{
    public int DetailId { get; set; }

    public int StockMovementId { get; set; }

    public string ProductSku { get; set; } = null!;

    public string? BatchNumber { get; set; }

    public int Quantity { get; set; }

    public decimal CostPrice { get; set; }

    public virtual Product ProductSkuNavigation { get; set; } = null!;

    public virtual StockMovement StockMovement { get; set; } = null!;
}
