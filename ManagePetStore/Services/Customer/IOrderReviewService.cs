using ManagePetStore.Areas.Customer.Models;

namespace ManagePetStore.Services.Customer;

public interface IOrderReviewService
{
    bool HasReviewed(int customerId, string orderId);
    HashSet<string> GetReviewedOrderIds(int customerId);
    Task<(bool Success, string Message)> SubmitReviewAsync(int customerId, OrderReviewSubmitModel model);
}
