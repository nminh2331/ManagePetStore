namespace ManagePetStore.Models.CustomerModels;

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
    public List<CartLineItemViewModel> VisibleItems { get; set; } = [];
    public string SearchTerm { get; set; } = "";
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 5;
    public int TotalFilteredItems { get; set; }
    public int TotalPages { get; set; }
    public int FilteredQuantity { get; set; }
    public decimal Subtotal => Items.Sum(i => i.LineTotal);
    public decimal ShippingFee => Items.Any(i => !i.Sku.StartsWith("SPA-SVC-", System.StringComparison.OrdinalIgnoreCase)) ? 30000m : 0m;
    public decimal VoucherDiscount { get; set; }
    public string? AppliedVoucherCode { get; set; }
    public decimal GrandTotal => Subtotal + ShippingFee - VoucherDiscount;
    public int TotalQuantity => Items.Sum(i => i.Quantity);
    public bool HasFilter => !string.IsNullOrWhiteSpace(SearchTerm);
    public bool HasPreviousPage => Page > 1;
    public bool HasNextPage => Page < TotalPages;
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

