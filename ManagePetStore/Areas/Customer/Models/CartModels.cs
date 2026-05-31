namespace ManagePetStore.Areas.Customer.Models;

public class CartSessionItem
{
    public string Sku { get; set; } = "";
    public string Name { get; set; } = "";
    public decimal Price { get; set; }
    public string ImageUrl { get; set; } = "";
    public int Quantity { get; set; }
    public int MaxStock { get; set; }
}

public class CartLineItemViewModel
{
    public string Sku { get; set; } = "";
    public string Name { get; set; } = "";
    public string ImageUrl { get; set; } = "";
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
    public int MaxStock { get; set; }
    public decimal LineTotal => UnitPrice * Quantity;
}

public class CartPageViewModel
{
    public List<CartLineItemViewModel> Items { get; set; } = [];
    public decimal Subtotal => Items.Sum(i => i.LineTotal);
    public decimal ShippingFee => Items.Count > 0 ? 30000m : 0m;
    public decimal VoucherDiscount { get; set; }
    public string? AppliedVoucherCode { get; set; }
    public decimal GrandTotal => Subtotal + ShippingFee - VoucherDiscount;
    public int TotalQuantity => Items.Sum(i => i.Quantity);
}

public class CartProductInfo
{
    public string Sku { get; set; } = "";
    public string Name { get; set; } = "";
    public decimal Price { get; set; }
    public string ImageUrl { get; set; } = "";
    public int Stock { get; set; }
    public bool InStock => Stock > 0;
}
