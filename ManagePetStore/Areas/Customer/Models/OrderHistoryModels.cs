namespace ManagePetStore.Areas.Customer.Models;

public class CustomerSidebarViewModel
{
    public ManagePetStore.Models.User User { get; set; } = null!;
    public ManagePetStore.Models.Customer Customer { get; set; } = null!;
    public string ActiveNav { get; set; } = "";
}

public class OrderHistoryPageViewModel : CustomerSidebarViewModel
{
    public List<OrderListItemViewModel> Orders { get; set; } = [];
    public List<OrderListItemViewModel> VisibleOrders { get; set; } = [];
    public string SearchTerm { get; set; } = "";
    public string StatusFilter { get; set; } = "all";
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 5;
    public int TotalFilteredItems { get; set; }
    public int TotalPages { get; set; }
    public bool HasPreviousPage => Page > 1;
    public bool HasNextPage => Page < TotalPages;
}

public class OrderListItemViewModel
{
    public string OrderId { get; set; } = "";
    public string DisplayOrderId { get; set; } = "";
    public DateTime OrderDate { get; set; }
    public decimal Total { get; set; }
    public string Status { get; set; } = "";
    public string StatusKey { get; set; } = "";
    public string? CancelReason { get; set; }
    public bool CanConfirmReceived { get; set; }
    public bool CanReview { get; set; }
    public bool HasReviewed { get; set; }
}

public class OrderDetailPageViewModel : CustomerSidebarViewModel
{
    public OrderDetailViewModel Order { get; set; } = new();
}

public class OrderDetailViewModel
{
    public string OrderId { get; set; } = "";
    public string DisplayOrderId { get; set; } = "";
    public DateTime OrderDate { get; set; }
    public decimal Subtotal { get; set; }
    public decimal Discount { get; set; }
    public decimal Total { get; set; }
    public string PaymentMethod { get; set; } = "";
    public string Status { get; set; } = "";
    public string StatusKey { get; set; } = "";
    public string? CancelReason { get; set; }
    public DateTime? CanceledAt { get; set; }
    public bool CanConfirmReceived { get; set; }
    public List<OrderDetailItemViewModel> Items { get; set; } = [];
}

public class OrderDetailItemViewModel
{
    public string? ProductSku { get; set; }
    public string ProductName { get; set; } = "";
    public string? ImageUrl { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal => UnitPrice * Quantity;
}
