using System.Security.Claims;
using ManagePetStore.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace ManagePetStore.Hubs;

[Authorize]
public class HotelCareHub : Hub
{
    private readonly PetStoreManagementContext _context;

    // [nam] Khởi tạo SignalR hub cho các cập nhật chăm sóc thời gian thực.
    public HotelCareHub(PetStoreManagementContext context)
    {
        _context = context;
    }

    // [nam] Đưa kết nối của khách vào nhóm riêng để chỉ nhận đúng thông báo.
    public override async Task OnConnectedAsync()
    {
        var userIdClaim = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (int.TryParse(userIdClaim, out var userId))
        {
            var customerId = await _context.Customers
                .Where(customer => customer.UserId == userId)
                .Select(customer => (int?)customer.CustomerId)
                .FirstOrDefaultAsync();

            if (customerId.HasValue)
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(customerId.Value));
            }
        }

        await base.OnConnectedAsync();
    }

    // [nam] Tạo tên nhóm SignalR ổn định theo CustomerId.
    public static string GroupName(int customerId) => $"hotel-care-customer-{customerId}";
}
