using System;
using System.Collections.Generic;

namespace ManagePetStore.Models;

public partial class ReturnRequestItem
{
    public int ReturnItemId { get; set; }

    public int RequestId { get; set; }

    public string Sku { get; set; } = null!;

    public int Quantity { get; set; }

    public decimal RefundPrice { get; set; }

    public virtual ReturnRequest Request { get; set; } = null!;

    public virtual Product SkuNavigation { get; set; } = null!;
}
