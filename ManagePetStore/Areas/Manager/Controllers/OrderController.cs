
// HÀ HOÀNG HIỆP CODE -- PHẦN DUYỆT / HỦY ĐƠN HÀNG BÊN MANAGER 
using System.Security.Claims;
using ManagePetStore.Areas.Manager.Models;
using ManagePetStore.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ManagePetStore.Areas.Manager.Controllers;

[Area("Manager")]
[Authorize(Roles = "manager")]
public class OrderController : Controller
{
    private readonly PetStoreManagementContext _context;

    public OrderController(PetStoreManagementContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? searchTerm, string statusFilter = "all", int page = 1)
    {
        ViewData["ManagerNav"] = "approval";


        //Query tất cả đơn hàng mua sản phẩm tạo từ customer
        var allOrders = await _context.Orders
            .Include(o => o.Customer)  //lấy luôn thông tin khách hàng liên quan
            .Include(o => o.OrderItems).ThenInclude(oi => oi.ProductSkuNavigation)
            .Include(o => o.OrderItems).ThenInclude(oi => oi.SpaService)
            .Where(o => o.OrderId.StartsWith("ORD-") && o.OrderItems.All(oi => oi.ProductSku != null && oi.SpaServiceId == null && oi.RoomTypeId == null))
            .OrderByDescending(o => o.Date)  //đơn mới nhất lên trên.
            .ToListAsync();  //thực thi query và lấy danh sách.

        var visibleOrders = allOrders
            .Where(o => OrderStatusHelper.IsPending(o.Status) || OrderStatusHelper.IsRejected(o.Status) || OrderStatusHelper.IsCancelled(o.Status))
            .ToList();

        return View(BuildPageModel("approval", visibleOrders, searchTerm, statusFilter, page));
    }
    //Màn Delivery: giao hàng / hoàn thành

    [HttpGet]
    public async Task<IActionResult> Delivery(string? searchTerm, string statusFilter = "all", int page = 1)
    {
        ViewData["ManagerNav"] = "delivery";

        var allOrders = await _context.Orders
            .Include(o => o.Customer)
            .Include(o => o.OrderItems).ThenInclude(oi => oi.ProductSkuNavigation)
            .Include(o => o.OrderItems).ThenInclude(oi => oi.SpaService)
            .Where(o => o.OrderId.StartsWith("ORD-") && o.OrderItems.All(oi => oi.ProductSku != null && oi.SpaServiceId == null && oi.RoomTypeId == null))
            .OrderByDescending(o => o.Date)
            .ToListAsync();
        //Chỉ giữ các đơn thuộc nhánh giao hàng
        var visibleOrders = allOrders
            .Where(o => OrderStatusHelper.IsApproved(o.Status) || OrderStatusHelper.IsDelivering(o.Status) || OrderStatusHelper.IsCompleted(o.Status))
            .ToList();

        return View(BuildPageModel("delivery", visibleOrders, searchTerm, statusFilter, page));  //Trả về view
    }


    //Action Approve


        [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(string orderId, string? searchTerm, string statusFilter = "all", int page = 1)
    {
        var order = await _context.Orders.FirstOrDefaultAsync(o => o.OrderId == orderId);  //Tìm đơn theo OrderId.
        if (order == null)
        {
            TempData["ErrorMessage"] = "Không tìm thấy đơn hàng.";
            return RedirectToAction(nameof(Index), new { searchTerm, statusFilter, page });
        }

        if (!OrderStatusHelper.IsPending(order.Status))
        {
            TempData["ErrorMessage"] = "Chỉ có thể phê duyệt đơn hàng đang chờ xử lý.";
            return RedirectToAction(nameof(Index), new { searchTerm, statusFilter, page });
        }

        order.Status = OrderStatusHelper.Approved;
        await _context.SaveChangesAsync();
        TempData["SuccessMessage"] = $"Đã phê duyệt đơn hàng {FormatDisplayOrderId(orderId)}.";
        return RedirectToAction(nameof(Index), new { searchTerm, statusFilter, page });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(string orderId, string cancelReason, string? searchTerm, string statusFilter = "all", int page = 1)
    {
        if (string.IsNullOrWhiteSpace(cancelReason))  //Kiểm tra lý do từ chối


        {
            TempData["ErrorMessage"] = "Vui lòng nhập lý do từ chối đơn hàng.";
            return RedirectToAction(nameof(Index), new { searchTerm, statusFilter, page });
        }

        var order = await _context.Orders.FirstOrDefaultAsync(o => o.OrderId == orderId);
        if (order == null)
        {
            TempData["ErrorMessage"] = "Không tìm thấy đơn hàng.";
            return RedirectToAction(nameof(Index), new { searchTerm, statusFilter, page });
        }

        if (!OrderStatusHelper.IsPending(order.Status))
        {
            TempData["ErrorMessage"] = "Chỉ có thể từ chối đơn hàng đang chờ xử lý.";
            return RedirectToAction(nameof(Index), new { searchTerm, statusFilter, page });
        }

        var managerName = User.FindFirst("FullName")?.Value  //Lấy tên manager đang thao tác
            ?? User.FindFirst(ClaimTypes.Name)?.Value
            ?? "Quản lý";

        order.Status = OrderStatusHelper.Rejected;
        order.CancelReason = cancelReason.Trim();
        order.CanceledBy = managerName;
        order.CanceledAt = DateTime.Now;

        await _context.SaveChangesAsync();
        await ManagePetStore.Services.Customer.CustomerRewardHelper.RecalculateCustomerPointsAndTierAsync(order.CustomerId, _context);
        TempData["SuccessMessage"] = $"Đã từ chối đơn hàng {FormatDisplayOrderId(orderId)}.";
        return RedirectToAction(nameof(Index), new { searchTerm, statusFilter, page });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Ship(string orderId, string? searchTerm, string statusFilter = "all", int page = 1)
    {
        var order = await _context.Orders.FirstOrDefaultAsync(o => o.OrderId == orderId);
        if (order == null)
        {
            TempData["ErrorMessage"] = "Không tìm thấy đơn hàng.";
            return RedirectToAction(nameof(Delivery), new { searchTerm, statusFilter, page });
        }

        if (!OrderStatusHelper.IsApproved(order.Status))
        {
            TempData["ErrorMessage"] = "Chỉ có thể giao hàng khi đơn đã được phê duyệt.";
            return RedirectToAction(nameof(Delivery), new { searchTerm, statusFilter, page });
        }

        order.Status = OrderStatusHelper.Delivering;
        await _context.SaveChangesAsync();
        TempData["SuccessMessage"] = $"Đơn hàng {FormatDisplayOrderId(orderId)} đã chuyển sang trạng thái đang giao hàng.";
        return RedirectToAction(nameof(Delivery), new { searchTerm, statusFilter, page });
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


    //Đây là hàm build model chung cho cả Index và Delivery.
    private static OrderManagementPageViewModel BuildPageModel(
        string activeTab,
        IEnumerable<Order> visibleOrders,
        string? searchTerm,
        string? statusFilter,
        int page)
    {
        var sourceOrders = visibleOrders.ToList();
        var normalizedSearch = searchTerm?.Trim() ?? "";
        var normalizedStatus = string.IsNullOrWhiteSpace(statusFilter) ? "all" : statusFilter.Trim().ToLowerInvariant();

        IEnumerable<Order> filteredOrders = sourceOrders;
      
        
        
        //Lọc theo từ khóa
        if (!string.IsNullOrWhiteSpace(normalizedSearch))
        {
            filteredOrders = filteredOrders.Where(o =>
                o.OrderId.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase) ||
                FormatDisplayOrderId(o.OrderId).Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase) ||
                (o.Customer?.FullName?.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (o.Customer?.Phone?.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase) ?? false) ||
                o.PaymentMethod.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase));
        }
        //Lọc theo trạng thái tùy tab
        filteredOrders = normalizedStatus switch
        {
            "pending" when activeTab == "approval" => filteredOrders.Where(o => OrderStatusHelper.IsPending(o.Status)),
            "rejected" when activeTab == "approval" => filteredOrders.Where(o => OrderStatusHelper.IsRejected(o.Status) || OrderStatusHelper.IsCancelled(o.Status)),
            "approved" when activeTab == "delivery" => filteredOrders.Where(o => OrderStatusHelper.IsApproved(o.Status)),
            "delivering" when activeTab == "delivery" => filteredOrders.Where(o => OrderStatusHelper.IsDelivering(o.Status)),
            "completed" when activeTab == "delivery" => filteredOrders.Where(o => OrderStatusHelper.IsCompleted(o.Status)),
            _ => filteredOrders
        };

        var filteredOrderList = filteredOrders.ToList();
        var pageSize = activeTab == "delivery" ? 5 : 8;
        var currentPage = page < 1 ? 1 : page;
        var totalFilteredItems = filteredOrderList.Count;
        var totalPages = totalFilteredItems == 0 ? 0 : (int)Math.Ceiling(totalFilteredItems / (double)pageSize);

        if (totalPages > 0 && currentPage > totalPages)
        {
            currentPage = totalPages;
        }

        return new OrderManagementPageViewModel
        {
            ActiveTab = activeTab,
            TotalCount = sourceOrders.Count,
            PendingCount = sourceOrders.Count(o => OrderStatusHelper.IsPending(o.Status)),
            ApprovedCount = sourceOrders.Count(o => OrderStatusHelper.IsApproved(o.Status)),
            DeliveringCount = sourceOrders.Count(o => OrderStatusHelper.IsDelivering(o.Status)),
            CompletedCount = sourceOrders.Count(o => OrderStatusHelper.IsCompleted(o.Status)),
            RejectedCount = sourceOrders.Count(o => OrderStatusHelper.IsRejected(o.Status) || OrderStatusHelper.IsCancelled(o.Status)),
            SearchTerm = normalizedSearch,
            StatusFilter = normalizedStatus,
            Page = totalPages == 0 ? 1 : currentPage,
            PageSize = pageSize,
            TotalFilteredItems = totalFilteredItems,
            TotalPages = totalPages,
            Orders = filteredOrderList
                .Skip((currentPage - 1) * pageSize)
                .Take(pageSize)
                .Select(MapOrder)
                .ToList()
        };
    }

    private static OrderManagementListItemViewModel MapOrder(Order order)
    {
        var statusKey = OrderStatusHelper.ResolveStatusKey(order.Status);
        var statusLabel = OrderStatusHelper.DisplayLabel(order.Status);

        if (statusKey == "pending" && order.PaymentMethod == "Thanh toán online")
        {
            statusLabel = "ĐÃ THANH TOÁN ONLINE, CHỜ XỬ LÝ";
        }

        // Tạo chuỗi mô tả sản phẩm
        var itemsList = new List<string>();
        foreach (var item in order.OrderItems)
        {
            string itemName = "Sản phẩm";
            if (item.ProductSkuNavigation != null)
            {
                itemName = item.ProductSkuNavigation.Name;
            }
            else if (item.SpaService != null)
            {
                itemName = item.SpaService.Name;
            }
            else if (!string.IsNullOrEmpty(item.ProductSku))
            {
                itemName = item.ProductSku;
            }
            itemsList.Add($"{itemName} (x{item.Quantity})");
        }
        var itemsSummary = string.Join(", ", itemsList);

        return new OrderManagementListItemViewModel
        {
            OrderId = order.OrderId,
            DisplayOrderId = FormatDisplayOrderId(order.OrderId),
            CustomerName = order.Customer?.FullName ?? "Khách hàng",
            CustomerPhone = order.Customer?.Phone,
            OrderDate = order.Date,
            Total = order.Total,
            PaymentMethod = order.PaymentMethod,
            StatusKey = statusKey,
            StatusLabel = statusLabel,
            CancelReason = order.CancelReason != null && order.CancelReason.StartsWith("VOUCHER:") ? null : order.CancelReason,
            CanApprove = statusKey == "pending",
            CanReject = statusKey == "pending",
            CanShip = statusKey == "approved",
            CanceledAt = order.CanceledAt,
            CanceledBy = order.CanceledBy,
            ItemsSummary = itemsSummary
        };
    }
}
