
// HÀ HOÀNG HIỆP CODE - PHẦN CHI TIẾT ĐƠN HÀNG --

using System.Security.Claims;
using ManagePetStore.Areas.Customer.Models;
using ManagePetStore.Services.Customer;
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
    public async Task<IActionResult> Index(string? searchTerm, string statusFilter = "all", int page = 1)
    {
        //lấy thông tin customer đang đăng nhập
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
        var mappedOrders = orders.Select(o => MapToListItem(o, reviewedOrderIds)).ToList();
        var normalizedSearch = searchTerm?.Trim() ?? "";
        var normalizedStatus = string.IsNullOrWhiteSpace(statusFilter) ? "all" : statusFilter.Trim().ToLowerInvariant();

        IEnumerable<OrderListItemViewModel> filteredOrders = mappedOrders;

        if (!string.IsNullOrWhiteSpace(normalizedSearch))
        {
            filteredOrders = filteredOrders.Where(o =>
                o.OrderId.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase) ||
                o.DisplayOrderId.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase) ||
                o.Status.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase));
        }

        filteredOrders = normalizedStatus switch
        {
            "pending" => filteredOrders.Where(o => o.StatusKey == "pending"),
            "approved" => filteredOrders.Where(o => o.StatusKey == "approved"),
            "delivering" => filteredOrders.Where(o => o.StatusKey == "delivering"),
            "completed" => filteredOrders.Where(o => o.StatusKey == "completed"),
            "cancelled" => filteredOrders.Where(o => o.StatusKey == "cancelled" || o.StatusKey == "rejected"),
            _ => filteredOrders
        };

        var filteredOrderList = filteredOrders.ToList();
        var currentPage = page < 1 ? 1 : page;
        var totalFilteredItems = filteredOrderList.Count;
        var totalPages = totalFilteredItems == 0 ? 0 : (int)Math.Ceiling(totalFilteredItems / (double)new OrderHistoryPageViewModel().PageSize);

        if (totalPages > 0 && currentPage > totalPages)
        {
            currentPage = totalPages;
        }

        var model = new OrderHistoryPageViewModel
        {
            User = layout.User,
            Customer = layout.Customer,
            ActiveNav = layout.ActiveNav,
            Orders = mappedOrders,
            SearchTerm = normalizedSearch,
            StatusFilter = normalizedStatus,
            Page = totalPages == 0 ? 1 : currentPage,
            TotalFilteredItems = totalFilteredItems,
            TotalPages = totalPages
        };

        model.VisibleOrders = filteredOrderList
            .Skip((model.Page - 1) * model.PageSize)
            .Take(model.PageSize)
            .ToList();

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SubmitReview(string orderId, int rating, string comment, string? searchTerm, string statusFilter = "all", int page = 1)
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
        return RedirectToAction(nameof(Index), new { searchTerm, statusFilter, page });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ConfirmReceived(string orderId, string? returnAction, string? searchTerm, string statusFilter = "all", int page = 1)
    {
        if (string.IsNullOrWhiteSpace(orderId))
        {
            TempData["ErrorMessage"] = "Không xác định được đơn hàng cần xác nhận.";
            return RedirectAfterConfirmation(orderId, returnAction, searchTerm, statusFilter, page);
        }

        var layout = await BuildSidebarViewModelAsync("orders");
        if (layout == null)
        {
            return RedirectToAction("Login", "Account", new { area = "Customer" });
        }

        var order = await _context.Orders
            .FirstOrDefaultAsync(o => o.OrderId == orderId && o.CustomerId == layout.Customer.CustomerId);

        if (order == null)
        {
            TempData["ErrorMessage"] = "Không tìm thấy đơn hàng hoặc bạn không có quyền thao tác.";
            return RedirectAfterConfirmation(orderId, returnAction, searchTerm, statusFilter, page);
        }

        if (OrderStatusHelper.ResolveStatusKey(order.Status) != "delivering")
        {
            TempData["ErrorMessage"] = "Chỉ có thể xác nhận với đơn hàng đang giao.";
            return RedirectAfterConfirmation(orderId, returnAction, searchTerm, statusFilter, page);
        }

        order.Status = "Đã hoàn thành";

        // Đồng bộ trạng thái thanh toán của các Spa Booking đi kèm đơn hàng (nếu có)
        var spaBookings = await _context.SpaBookings
            .Where(sb => sb.CustomerId == layout.Customer.CustomerId && sb.Notes != null && sb.Notes.Contains(orderId))
            .ToListAsync();

        foreach (var booking in spaBookings)
        {
            booking.Status = "Đã thanh toán";
        }

        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Đơn hàng {FormatDisplayOrderId(orderId)} đã được xác nhận nhận hàng.";
        return RedirectAfterConfirmation(orderId, returnAction, searchTerm, statusFilter, page);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(string orderId, string cancelReason, string? returnAction, string? searchTerm, string statusFilter = "all", int page = 1)
    {
        if (string.IsNullOrWhiteSpace(orderId))
        {
            TempData["ErrorMessage"] = "Không xác định được đơn hàng cần hủy.";
            return RedirectAfterConfirmation(orderId, returnAction, searchTerm, statusFilter, page);
        }

        if (string.IsNullOrWhiteSpace(cancelReason))
        {
            TempData["ErrorMessage"] = "Vui lòng chọn hoặc nhập lý do hủy đơn.";
            return RedirectAfterConfirmation(orderId, returnAction, searchTerm, statusFilter, page);
        }

        var layout = await BuildSidebarViewModelAsync("orders");
        if (layout == null)
        {
            return RedirectToAction("Login", "Account", new { area = "Customer" });
        }

        var order = await _context.Orders
            .Include(o => o.OrderItems)
            .FirstOrDefaultAsync(o => o.OrderId == orderId && o.CustomerId == layout.Customer.CustomerId);

        if (order == null)
        {
            TempData["ErrorMessage"] = "Không tìm thấy đơn hàng hoặc bạn không có quyền thao tác.";
            return RedirectAfterConfirmation(orderId, returnAction, searchTerm, statusFilter, page);
        }

        var statusKey = OrderStatusHelper.ResolveStatusKey(order.Status);
        if (statusKey != "pending")
        {
            TempData["ErrorMessage"] = "Chỉ có thể hủy đơn hàng ở trạng thái chờ xử lý hoặc chờ thanh toán.";
            return RedirectAfterConfirmation(orderId, returnAction, searchTerm, statusFilter, page);
        }

        // Hoàn lại số lượng tồn kho cho sản phẩm
        foreach (var item in order.OrderItems)
        {
            if (!string.IsNullOrEmpty(item.ProductSku))
            {
                var product = await _context.Products.FirstOrDefaultAsync(p => p.Sku == item.ProductSku);
                if (product != null)
                {
                    product.Stock += item.Quantity;
                    _context.Entry(product).State = EntityState.Modified;
                }
            }
        }

        // Hủy các Spa Booking đi kèm đơn hàng (nếu có)
        var spaBookings = await _context.SpaBookings
            .Where(sb => sb.CustomerId == layout.Customer.CustomerId && sb.Notes != null && sb.Notes.Contains(orderId))
            .ToListAsync();

        foreach (var booking in spaBookings)
        {
            booking.Status = "Đã hủy";
            _context.Entry(booking).State = EntityState.Modified;
        }

        order.Status = "Đã hủy";
        order.CancelReason = cancelReason.Trim();
        order.CanceledBy = layout.Customer.FullName;
        order.CanceledAt = DateTime.Now;

        _context.Entry(order).State = EntityState.Modified;
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Đơn hàng {FormatDisplayOrderId(orderId)} đã được hủy thành công.";
        return RedirectAfterConfirmation(orderId, returnAction, searchTerm, statusFilter, page);
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
        // Quay lại Details: query đơn hàng
        var order = await _context.Orders
            .Include(o => o.OrderItems)
            .FirstOrDefaultAsync(o => o.OrderId == id && o.CustomerId == layout.Customer.CustomerId);

        if (order == null)
        {
            TempData["ErrorMessage"] = "Không tìm thấy đơn hàng hoặc bạn không có quyền xem.";
            return RedirectToAction(nameof(Index));
        }

        ViewBag.HasReturnRequest = await _context.ReturnRequests.AnyAsync(r => r.OrderId == id);
        // Lấy thêm tên và ảnh sản phẩm
        var productInfo = await LoadProductInfoBySkusAsync(
            order.OrderItems
                .Select(i => i.ProductSku)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Cast<string>());

        var model = new OrderDetailPageViewModel
        {
            User = layout.User,
            Customer = layout.Customer,
            ActiveNav = layout.ActiveNav,
            Order = MapToDetail(order, productInfo)
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

        //Query user và customer
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
        var statusKey = OrderStatusHelper.ResolveStatusKey(order.Status);
        var hasReviewed = reviewedOrderIds.Contains(order.OrderId);
        var statusLabel = OrderStatusHelper.FormatStatusLabel(statusKey, order.Status);

        if (statusKey == "pending" && order.PaymentMethod == "Thanh toán online")
        {
            statusLabel = "ĐÃ THANH TOÁN ONLINE, CHỜ XỬ LÝ";
        }

        return new OrderListItemViewModel
        {
            OrderId = order.OrderId,
            DisplayOrderId = FormatDisplayOrderId(order.OrderId),
            OrderDate = order.Date,
            Total = order.Total,
            Status = statusLabel,
            StatusKey = statusKey,
            CancelReason = order.CancelReason != null && order.CancelReason.StartsWith("VOUCHER:") ? null : order.CancelReason,
            CanConfirmReceived = statusKey == "delivering",
            CanReview = statusKey == "completed" && !hasReviewed,
            HasReviewed = hasReviewed,
            CanCancel = statusKey == "pending"
        };
    }

    private static OrderDetailViewModel MapToDetail(
        Order order,
        IReadOnlyDictionary<string, (string Name, string? ImageUrl)> productInfo)
    {
        var statusKey = OrderStatusHelper.ResolveStatusKey(order.Status);
        var statusLabel = OrderStatusHelper.FormatStatusLabel(statusKey, order.Status);

        if (statusKey == "pending" && order.PaymentMethod == "Thanh toán online")
        {
            statusLabel = "ĐÃ THANH TOÁN ONLINE, CHỜ XỬ LÝ";
        }

        return new OrderDetailViewModel
        {
            OrderId = order.OrderId,
            DisplayOrderId = FormatDisplayOrderId(order.OrderId),
            OrderDate = order.Date,
            Subtotal = order.Subtotal,
            Discount = order.Discount,
            Total = order.Total,
            PaymentMethod = order.PaymentMethod,
            Status = statusLabel,
            StatusKey = statusKey,
            CancelReason = order.CancelReason != null && order.CancelReason.StartsWith("VOUCHER:") ? null : order.CancelReason,
            CanceledAt = order.CanceledAt,
            CanConfirmReceived = statusKey == "delivering",
            CanCancel = statusKey == "pending",
            Items = order.OrderItems.Select(item =>
            {
                var sku = item.ProductSku ?? "";
                productInfo.TryGetValue(sku, out var info);

                return new OrderDetailItemViewModel
                {
                    ProductSku = item.ProductSku,
                    ProductName = !string.IsNullOrEmpty(info.Name) ? info.Name : (sku.Length > 0 ? sku : "Sản phẩm"),
                    ImageUrl = info.ImageUrl,
                    Quantity = item.Quantity,
                    UnitPrice = item.Price
                };
            }).ToList()
        };
    }

    private async Task<Dictionary<string, (string Name, string? ImageUrl)>> LoadProductInfoBySkusAsync(
        IEnumerable<string> skus)
    {
        var result = new Dictionary<string, (string Name, string? ImageUrl)>(StringComparer.OrdinalIgnoreCase);

        foreach (var sku in skus.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var row = await _context.Database
                .SqlQueryRaw<ProductInfoRow>(
                    "SELECT Sku, Name, ImageUrl FROM Products WHERE Sku = {0}",
                    sku)
                .FirstOrDefaultAsync();

            if (row != null)
            {
                result[sku] = (row.Name, row.ImageUrl);
            }
        }

        return result;
    }

    private sealed class ProductInfoRow
    {
        public string Sku { get; set; } = "";
        public string Name { get; set; } = "";
        public string? ImageUrl { get; set; }
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

    private IActionResult RedirectAfterConfirmation(string orderId, string? returnAction, string? searchTerm, string statusFilter, int page)
    {
        if (string.Equals(returnAction, nameof(Details), StringComparison.OrdinalIgnoreCase))
        {
            return RedirectToAction(nameof(Details), new { id = orderId });
        }

        return RedirectToAction(nameof(Index), new { searchTerm, statusFilter, page });
    }
}
