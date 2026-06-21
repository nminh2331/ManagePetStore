using ManagePetStore.Models.CustomerModels;

namespace ManagePetStore.Services.Customer;

public interface ICheckoutEmailService
{
    Task SendOrderConfirmationAsync(
        string toEmail,
        CheckoutSuccessViewModel order,
        IReadOnlyList<CartLineItemViewModel> items,
        string? orderNote);
}

