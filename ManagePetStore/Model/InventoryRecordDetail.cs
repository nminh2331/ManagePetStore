using System;
using System.Collections.Generic;

namespace ManagePetStore.Model;

public partial class InventoryRecordDetail
{
    public int DetailId { get; set; }

    public int RecordId { get; set; }

    public int ProductId { get; set; }

    public int Quantity { get; set; }

    public decimal UnitPrice { get; set; }

    public virtual Product Product { get; set; } = null!;

    public virtual InventoryRecord Record { get; set; } = null!;
}
