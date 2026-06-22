using ManagePetStore.Areas.Customer.Models;

namespace ManagePetStore.Areas.Customer.Services;

public interface ICheckoutEmailService
{
    Task SendOrderConfirmationAsync(
        string toEmail,
        CheckoutSuccessViewModel order,
        IReadOnlyList<CartLineItemViewModel> items,
        string? orderNote);
}
