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

    public NotificationController(PetStoreManagementContext context)
    {
        _context = context;
    }

    [HttpGet("")]
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
