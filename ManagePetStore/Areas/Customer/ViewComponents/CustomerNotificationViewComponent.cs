using System.Security.Claims;
using ManagePetStore.Areas.Customer.Models;
using ManagePetStore.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ManagePetStore.Areas.Customer.ViewComponents;

public class CustomerNotificationViewComponent : ViewComponent
{
    private readonly PetStoreManagementContext _context;

    // [nam] Khởi tạo view component thông báo dùng trên thanh điều hướng khách hàng.
    public CustomerNotificationViewComponent(PetStoreManagementContext context)
    {
        _context = context;
    }

    // [nam] Tải số thông báo chưa đọc và năm thông báo mới nhất cho menu.
    public async Task<IViewComponentResult> InvokeAsync()
    {
        var claim = UserClaimsPrincipal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!int.TryParse(claim, out var userId))
        {
            return View(new CustomerNotificationMenuViewModel());
        }

        var customerId = await _context.Customers
            .Where(customer => customer.UserId == userId)
            .Select(customer => (int?)customer.CustomerId)
            .FirstOrDefaultAsync();
        if (!customerId.HasValue)
        {
            return View(new CustomerNotificationMenuViewModel());
        }

        var model = new CustomerNotificationMenuViewModel
        {
            IsCustomer = true,
            UnreadCount = await _context.CustomerNotifications
                .CountAsync(notification => notification.CustomerId == customerId && !notification.IsRead),
            Items = await _context.CustomerNotifications
                .AsNoTracking()
                .Where(notification => notification.CustomerId == customerId)
                .OrderByDescending(notification => notification.CreatedAt)
                .Take(5)
                .Select(notification => new CustomerNotificationItemViewModel
                {
                    NotificationId = notification.NotificationId,
                    Title = notification.Title,
                    Message = notification.Message,
                    CreatedAt = notification.CreatedAt,
                    IsRead = notification.IsRead
                })
                .ToListAsync()
        };

        return View(model);
    }
}
