using System.Security.Claims;
using ManagePetStore.Areas.Customer.Models;
using ManagePetStore.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ManagePetStore.Areas.Customer.Controllers;

[Area("Customer")]
[Authorize]
[Route("Customer/Notifications")]
public class NotificationController : Controller
{
    private readonly PetStoreManagementContext _context;

    // [nam] Khởi tạo controller thông báo với ngữ cảnh dữ liệu của hệ thống.
    public NotificationController(PetStoreManagementContext context)
    {
        _context = context;
    }

    [HttpGet("")]
    // [nam] Hiển thị các thông báo gần nhất và số lượng chưa đọc của khách hàng.
    public async Task<IActionResult> Index()
    {
        var identity = await GetIdentityAsync();
        if (identity == null)
        {
            return Forbid();
        }

        var notifications = await _context.CustomerNotifications
            .AsNoTracking()
            .Where(notification => notification.CustomerId == identity.Value.Customer.CustomerId)
            .OrderByDescending(notification => notification.CreatedAt)
            .Take(100)
            .Select(notification => new CustomerNotificationItemViewModel
            {
                NotificationId = notification.NotificationId,
                Title = notification.Title,
                Message = notification.Message,
                CreatedAt = notification.CreatedAt,
                IsRead = notification.IsRead
            })
            .ToListAsync();

        return View(new CustomerNotificationPageViewModel
        {
            User = identity.Value.User,
            Customer = identity.Value.Customer,
            ActiveNav = "notifications",
            Notifications = notifications,
            UnreadCount = notifications.Count(notification => !notification.IsRead)
        });
    }

    [HttpGet("Open/{id:long}")]
    // [nam] Đánh dấu một thông báo là đã đọc và chuyển đến liên kết nội bộ an toàn.
    public async Task<IActionResult> Open(long id)
    {
        var customerId = await GetCustomerIdAsync();
        if (!customerId.HasValue)
        {
            return Forbid();
        }

        var notification = await _context.CustomerNotifications
            .FirstOrDefaultAsync(item => item.NotificationId == id && item.CustomerId == customerId.Value);
        if (notification == null)
        {
            return NotFound();
        }

        if (!notification.IsRead)
        {
            notification.IsRead = true;
            notification.ReadAt = DateTime.Now;
            await _context.SaveChangesAsync();
        }

        return LocalRedirect(Url.IsLocalUrl(notification.LinkUrl)
            ? notification.LinkUrl!
            : "/Customer/Notifications");
    }

    [HttpPost("MarkAllRead")]
    [ValidateAntiForgeryToken]
    // [nam] Đánh dấu toàn bộ thông báo của khách hàng hiện tại là đã đọc.
    public async Task<IActionResult> MarkAllRead()
    {
        var customerId = await GetCustomerIdAsync();
        if (!customerId.HasValue)
        {
            return Forbid();
        }

        var unread = await _context.CustomerNotifications
            .Where(item => item.CustomerId == customerId.Value && !item.IsRead)
            .ToListAsync();
        foreach (var notification in unread)
        {
            notification.IsRead = true;
            notification.ReadAt = DateTime.Now;
        }
        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("Delete/{id:long}")]
    [ValidateAntiForgeryToken]
    // [nam] Xóa một thông báo theo ID và tính lại số lượng chưa đọc.
    public async Task<IActionResult> Delete(long id)
    {
        var customerId = await GetCustomerIdAsync();
        if (!customerId.HasValue)
        {
            return Forbid();
        }

        var notification = await _context.CustomerNotifications
            .FirstOrDefaultAsync(item => item.NotificationId == id && item.CustomerId == customerId.Value);

        if (notification != null)
        {
            _context.CustomerNotifications.Remove(notification);
            await _context.SaveChangesAsync();
        }

        var unreadCount = await _context.CustomerNotifications
            .CountAsync(item => item.CustomerId == customerId.Value && !item.IsRead);
        var totalCount = await _context.CustomerNotifications
            .CountAsync(item => item.CustomerId == customerId.Value);

        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest" || Request.Headers["Accept"].ToString().Contains("application/json"))
        {
            return Json(new { success = true, unreadCount, totalCount });
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost("DeleteAll")]
    [ValidateAntiForgeryToken]
    // [nam] Xóa toàn bộ thông báo của khách hàng hiện tại.
    public async Task<IActionResult> DeleteAll()
    {
        var customerId = await GetCustomerIdAsync();
        if (!customerId.HasValue)
        {
            return Forbid();
        }

        var notifications = await _context.CustomerNotifications
            .Where(item => item.CustomerId == customerId.Value)
            .ToListAsync();

        if (notifications.Count > 0)
        {
            _context.CustomerNotifications.RemoveRange(notifications);
            await _context.SaveChangesAsync();
        }

        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest" || Request.Headers["Accept"].ToString().Contains("application/json"))
        {
            return Json(new { success = true, unreadCount = 0, totalCount = 0 });
        }

        return RedirectToAction(nameof(Index));
    }

    // [nam] Lấy CustomerId từ claim của tài khoản đang đăng nhập.
    private async Task<int?> GetCustomerIdAsync()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!int.TryParse(claim, out var userId))
        {
            return null;
        }

        return await _context.Customers
            .Where(customer => customer.UserId == userId)
            .Select(customer => (int?)customer.CustomerId)
            .FirstOrDefaultAsync();
    }

    // [nam] Tải đồng thời thông tin User và Customer cho trang thông báo.
    private async Task<(User User, ManagePetStore.Models.Customer Customer)?> GetIdentityAsync()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!int.TryParse(claim, out var userId))
        {
            return null;
        }

        var user = await _context.Users
            .Include(item => item.Customer)
            .FirstOrDefaultAsync(item => item.UserId == userId);
        return user?.Customer == null ? null : (user, user.Customer);
    }
}
