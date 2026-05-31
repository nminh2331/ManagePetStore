using System.Text.Json;
using ManagePetStore.Areas.Customer.Models;
using ManagePetStore.Models;
using Microsoft.EntityFrameworkCore;

namespace ManagePetStore.Areas.Customer.Services;

public class OrderReviewService : IOrderReviewService
{
    private const string SessionKeyPrefix = "CustomerOrderReviews_";

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly PetStoreManagementContext _context;

    public OrderReviewService(IHttpContextAccessor httpContextAccessor, PetStoreManagementContext context)
    {
        _httpContextAccessor = httpContextAccessor;
        _context = context;
    }

    public bool HasReviewed(int customerId, string orderId)
    {
        return GetReviewedOrderIds(customerId).Contains(orderId);
    }

    public HashSet<string> GetReviewedOrderIds(int customerId)
    {
        var reviews = LoadReviews(customerId);
        return reviews.Select(r => r.OrderId).ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    public async Task<(bool Success, string Message)> SubmitReviewAsync(int customerId, OrderReviewSubmitModel model)
    {
        if (string.IsNullOrWhiteSpace(model.OrderId))
        {
            return (false, "Không xác định được đơn hàng cần đánh giá.");
        }

        if (model.Rating < 1 || model.Rating > 5)
        {
            return (false, "Vui lòng chọn số sao từ 1 đến 5.");
        }

        if (string.IsNullOrWhiteSpace(model.Comment))
        {
            return (false, "Vui lòng nhập ý kiến phản hồi.");
        }

        var order = await _context.Orders
            .FirstOrDefaultAsync(o => o.OrderId == model.OrderId && o.CustomerId == customerId);

        if (order == null)
        {
            return (false, "Không tìm thấy đơn hàng hoặc bạn không có quyền đánh giá.");
        }

        var statusKey = ResolveStatusKey(order.Status);
        if (statusKey != "completed")
        {
            return (false, "Chỉ có thể đánh giá đơn hàng đã hoàn thành.");
        }

        if (HasReviewed(customerId, model.OrderId))
        {
            return (false, "Bạn đã gửi đánh giá cho đơn hàng này.");
        }

        var reviews = LoadReviews(customerId);
        reviews.Add(new StoredOrderReview
        {
            OrderId = model.OrderId,
            CustomerId = customerId,
            Rating = model.Rating,
            Comment = model.Comment.Trim(),
            CreatedAt = DateTime.Now
        });

        SaveReviews(customerId, reviews);
        return (true, "Đã gửi đánh giá thành công!");
    }

    private List<StoredOrderReview> LoadReviews(int customerId)
    {
        var session = _httpContextAccessor.HttpContext?.Session;
        if (session == null)
        {
            return [];
        }

        var json = session.GetString($"{SessionKeyPrefix}{customerId}");
        if (string.IsNullOrEmpty(json))
        {
            return [];
        }

        return JsonSerializer.Deserialize<List<StoredOrderReview>>(json) ?? [];
    }

    private void SaveReviews(int customerId, List<StoredOrderReview> reviews)
    {
        var session = _httpContextAccessor.HttpContext?.Session;
        if (session == null)
        {
            return;
        }

        session.SetString($"{SessionKeyPrefix}{customerId}", JsonSerializer.Serialize(reviews));
    }

    private static string ResolveStatusKey(string? status)
    {
        var normalized = status?.Trim().ToLowerInvariant() ?? "";

        if (normalized.Contains("giao") && !normalized.Contains("hoàn"))
        {
            return "delivering";
        }

        if (normalized.Contains("chờ") ||
            normalized.Contains("cho") ||
            normalized.Contains("duyệt") ||
            normalized.Contains("duyet") ||
            normalized.Contains("xử lý") ||
            normalized.Contains("xu ly") ||
            normalized.Contains("pending"))
        {
            return "pending";
        }

        return "completed";
    }
}
