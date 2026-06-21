namespace ManagePetStore.Models.ManagerModels;

public class OrderManagementPageViewModel
{
    public string ActiveTab { get; set; } = "";
    public int TotalCount { get; set; }
    public int PendingCount { get; set; }
    public int ApprovedCount { get; set; }
    public int DeliveringCount { get; set; }
    public int CompletedCount { get; set; }
    public int RejectedCount { get; set; }
    public string SearchTerm { get; set; } = "";
    public string StatusFilter { get; set; } = "all";
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 8;
    public int TotalFilteredItems { get; set; }
    public int TotalPages { get; set; }
    public List<OrderManagementListItemViewModel> Orders { get; set; } = [];
    public bool HasPreviousPage => Page > 1;
    public bool HasNextPage => Page < TotalPages;
}

public class OrderManagementListItemViewModel
{
    public string OrderId { get; set; } = "";
    public string DisplayOrderId { get; set; } = "";
    public string CustomerName { get; set; } = "";
    public string? CustomerPhone { get; set; }
    public DateTime OrderDate { get; set; }
    public decimal Total { get; set; }
    public string PaymentMethod { get; set; } = "";
    public string StatusLabel { get; set; } = "";
    public string StatusKey { get; set; } = "";
    public string? CancelReason { get; set; }
    public bool CanApprove { get; set; }
    public bool CanReject { get; set; }
    public bool CanShip { get; set; }
}

