namespace ManagePetStore.Areas.Customer.Models;

public class OrderReviewSubmitModel
{
    public string OrderId { get; set; } = "";
    public int Rating { get; set; } = 5;
    public string Comment { get; set; } = "";
}

public class StoredOrderReview
{
    public string OrderId { get; set; } = "";
    public int CustomerId { get; set; }
    public int Rating { get; set; }
    public string Comment { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}

public class OrderReviewModalViewModel
{
    public string OrderId { get; set; } = "";
    public string DisplayOrderId { get; set; } = "";
}
