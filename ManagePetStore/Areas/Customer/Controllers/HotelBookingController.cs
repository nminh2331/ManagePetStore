using System.Security.Claims;
using ManagePetStore.Areas.Customer.Models;
using ManagePetStore.Models;
using ManagePetStore.Services;
using ManagePetStore.Services.Hotel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ManagePetStore.Areas.Customer.Controllers;

[Area("Customer")]
[Authorize]
public partial class HotelBookingController : Controller
{
    private static readonly string[] BlockingStatuses = ["Đã đặt", "Active", "Đang ở"];

    private readonly PetStoreManagementContext _context;
    private readonly IHotelBookingHistoryService _historyService;
    private readonly IHotelAvailabilityService _availabilityService;
    private readonly IHotelBookingService _bookingService;
    private readonly ILogger<HotelBookingController> _logger;

    // [nam] Khởi tạo controller và các dịch vụ phục vụ quy trình đặt chuồng.
    public HotelBookingController(
        PetStoreManagementContext context,
        IHotelBookingHistoryService historyService,
        IHotelAvailabilityService availabilityService,
        IHotelBookingService bookingService,
        ILogger<HotelBookingController> logger)
    {
        _context = context;
        _historyService = historyService;
        _availabilityService = availabilityService;
        _bookingService = bookingService;
        _logger = logger;
    }

    // [nam] Lấy hồ sơ khách hàng tương ứng với tài khoản đang đăng nhập.
    private async Task<ManagePetStore.Models.Customer?> GetCurrentCustomerAsync()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out var userId))
        {
            return null;
        }

        return await _context.Customers.FirstOrDefaultAsync(c => c.UserId == userId);
    }

    // [nam] Dựng dữ liệu người dùng và khách hàng cho sidebar của khu vực Customer.
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


    // [nam] Quy đổi hạng thành viên thành tỷ lệ giảm giá khi đặt chuồng.
    private static decimal ResolveDiscountRate(string? membershipTier)
    {
        return HotelPricingPolicy.ResolveMembershipDiscountRate(membershipTier);
    }


    // [nam] Chuẩn hoá trạng thái booking thành khoá dùng cho giao diện và bộ lọc.
    private static string ResolveStatusKey(string? status)
    {
        return status?.Trim().ToLowerInvariant() switch
        {
            "đã đặt" => "reserved",
            "active" or "đang ở" => "active",
            "đã trả" => "completed",
            "đã hủy" or "cancelled" or "từ chối tiếp nhận" => "cancelled",
            _ => "other"
        };
    }
}
