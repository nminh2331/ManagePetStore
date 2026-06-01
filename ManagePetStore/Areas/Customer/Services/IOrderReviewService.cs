using ManagePetStore.Areas.Customer.Models;

namespace ManagePetStore.Areas.Customer.Services;

public interface IOrderReviewService
{
    bool HasReviewed(int customerId, string orderId);
    HashSet<string> GetReviewedOrderIds(int customerId);
    Task<(bool Success, string Message)> SubmitReviewAsync(int customerId, OrderReviewSubmitModel model);
}
