using System.Security.Claims;
using ManagePetStore.Areas.Customer.Models;
using ManagePetStore.Areas.Customer.Services;
using ManagePetStore.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ManagePetStore.Areas.Customer.Controllers;

[Area("Customer")]
[Authorize]
public class OrderController : Controller
{
    private readonly PetStoreManagementContext _context;
    private readonly IOrderReviewService _reviewService;

    public OrderController(PetStoreManagementContext context, IOrderReviewService reviewService)
    {
        _context = context;
        _reviewService = reviewService;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var layout = await BuildSidebarViewModelAsync("orders");
        if (layout == null)
        {
            return RedirectToAction("Login", "Account", new { area = "Customer" });
        }

        var orders = await _context.Orders
            .Where(o => o.CustomerId == layout.Customer.CustomerId)
            .OrderByDescending(o => o.Date)
            .ToListAsync();

        var reviewedOrderIds = _reviewService.GetReviewedOrderIds(layout.Customer.CustomerId);

        var model = new OrderHistoryPageViewModel
        {
            User = layout.User,
            Customer = layout.Customer,
            ActiveNav = layout.ActiveNav,
            Orders = orders.Select(o => MapToListItem(o, reviewedOrderIds)).ToList()
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SubmitReview(string orderId, int rating, string comment)
    {
        var layout = await BuildSidebarViewModelAsync("orders");
        if (layout == null)
        {
            return RedirectToAction("Login", "Account", new { area = "Customer" });
        }

        var (success, message) = await _reviewService.SubmitReviewAsync(
            layout.Customer.CustomerId,
            new OrderReviewSubmitModel
            {
                OrderId = orderId,
                Rating = rating,
                Comment = comment
            });

        TempData[success ? "SuccessMessage" : "ErrorMessage"] = message;
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Details(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return RedirectToAction(nameof(Index));
        }

        var layout = await BuildSidebarViewModelAsync("orders");
        if (layout == null)
        {
            return RedirectToAction("Login", "Account", new { area = "Customer" });
        }

        var order = await _context.Orders
            .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.ProductSkuNavigation)
            .FirstOrDefaultAsync(o => o.OrderId == id && o.CustomerId == layout.Customer.CustomerId);

        if (order == null)
        {
            TempData["ErrorMessage"] = "Không tìm thấy đơn hàng hoặc bạn không có quyền xem.";
            return RedirectToAction(nameof(Index));
        }

        var model = new OrderDetailPageViewModel
        {
            User = layout.User,
            Customer = layout.Customer,
            ActiveNav = layout.ActiveNav,
            Order = MapToDetail(order)
        };

        return View(model);
    }

    private async Task<CustomerSidebarViewModel?> BuildSidebarViewModelAsync(string activeNav)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out var userId))
        {
            return null;
        }

        var user = await _context.Users
            .Include(u => u.Role)
            .Include(u => u.Customer)
            .FirstOrDefaultAsync(u => u.UserId == userId);

        if (user?.Customer == null)
        {
            return null;
        }

        return new CustomerSidebarViewModel
        {
            User = user,
            Customer = user.Customer,
            ActiveNav = activeNav
        };
    }

    private static OrderListItemViewModel MapToListItem(Order order, HashSet<string> reviewedOrderIds)
    {
        var statusKey = ResolveStatusKey(order.Status);
        var hasReviewed = reviewedOrderIds.Contains(order.OrderId);

        return new OrderListItemViewModel
        {
            OrderId = order.OrderId,
            DisplayOrderId = FormatDisplayOrderId(order.OrderId),
            OrderDate = order.Date,
            Total = order.Total,
            Status = FormatStatusLabel(statusKey, order.Status),
            StatusKey = statusKey,
            CanReview = statusKey == "completed" && !hasReviewed,
            HasReviewed = hasReviewed
        };
    }

    private static OrderDetailViewModel MapToDetail(Order order)
    {
        var statusKey = ResolveStatusKey(order.Status);

        return new OrderDetailViewModel
        {
            OrderId = order.OrderId,
            DisplayOrderId = FormatDisplayOrderId(order.OrderId),
            OrderDate = order.Date,
            Subtotal = order.Subtotal,
            Discount = order.Discount,
            Total = order.Total,
            PaymentMethod = order.PaymentMethod,
            Status = FormatStatusLabel(statusKey, order.Status),
            StatusKey = statusKey,
            Items = order.OrderItems.Select(item => new OrderDetailItemViewModel
            {
                ProductSku = item.ProductSku,
                ProductName = item.ProductSkuNavigation?.Name ?? item.ProductSku ?? "Sản phẩm",
                ImageUrl = item.ProductSkuNavigation?.ImageUrl,
                Quantity = item.Quantity,
                UnitPrice = item.Price
            }).ToList()
        };
    }

    private static string FormatDisplayOrderId(string orderId)
    {
        var parts = orderId.Split('-', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 2)
        {
            return $"#OD-{parts[^1]}";
        }

        return $"#{orderId}";
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

    private static string FormatStatusLabel(string statusKey, string? originalStatus)
    {
        return statusKey switch
        {
            "pending" => "CHỜ XỬ LÝ",
            "delivering" => "ĐANG GIAO",
            "completed" => "HOÀN THÀNH",
            _ => string.IsNullOrWhiteSpace(originalStatus) ? "HOÀN THÀNH" : originalStatus.ToUpperInvariant()
        };
    }
}
