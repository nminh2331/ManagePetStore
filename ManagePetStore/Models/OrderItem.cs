using System;
using System.Collections.Generic;

namespace ManagePetStore.Models;

public partial class OrderItem
{
    public int OrderItemId { get; set; }

    public string OrderId { get; set; } = null!;

    public string? ProductSku { get; set; }

    public int? SpaServiceId { get; set; }

    public int? RoomTypeId { get; set; }

    public int Quantity { get; set; }

    public decimal Price { get; set; }

    public bool IsCombo { get; set; }

    public virtual Order Order { get; set; } = null!;

    public virtual Product? ProductSkuNavigation { get; set; }

    public virtual RoomType? RoomType { get; set; }

    public virtual SpaService? SpaService { get; set; }
}
