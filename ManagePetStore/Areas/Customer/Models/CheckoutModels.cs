namespace ManagePetStore.Areas.Customer.Models;

public class CheckoutViewModel
{
    public string FullName { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Email { get; set; } = "";
    public string ShippingAddress { get; set; } = "";
    public string? OrderNote { get; set; }
    public string PaymentMethod { get; set; } = "Cash";
    public string? VoucherCode { get; set; }
    public CartPageViewModel Cart { get; set; } = new();
}

public class CheckoutSuccessViewModel
{
    public string OrderId { get; set; } = "";
    public string FullName { get; set; } = "";
    public string Phone { get; set; } = "";
    public string ShippingAddress { get; set; } = "";
    public string ConfirmationEmail { get; set; } = "";
    public string PaymentMethod { get; set; } = "";
    public decimal Total { get; set; }
    public int ItemCount { get; set; }
}

public class AppliedVoucherSession
{
    public string Code { get; set; } = "";
    public decimal Discount { get; set; }
}
