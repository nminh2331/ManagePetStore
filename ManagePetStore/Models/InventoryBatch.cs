using System;
using System.Collections.Generic;

namespace ManagePetStore.Models;

public partial class InventoryBatch
{
    public int BatchId { get; set; }

    public string ProductSku { get; set; } = null!;

    public DateTime ReceivedDate { get; set; }

    public int Quantity { get; set; }

    public int CurrentQuantity { get; set; }

    public DateTime ExpiryDate { get; set; }

    public virtual Product ProductSkuNavigation { get; set; } = null!;
}
