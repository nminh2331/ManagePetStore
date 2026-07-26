using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using ManagePetStore.Hubs;
using ManagePetStore.Models;
using ManagePetStore.Areas.ServiceStaff.Models;
using ManagePetStore.Services;
using ManagePetStore.Services.Hotel;
using ManagePetStore.Services.Warehouse;
using CustomerEntity = ManagePetStore.Models.Customer;

namespace ManagePetStore.Areas.ServiceStaff.Controllers
{
    [Area("ServiceStaff")]
    [Authorize(Roles = "service,admin,manager")]
    [Route("SpaServices")]
    public partial class SpaCagesController : Controller
    {
        private static readonly string[] ActiveHotelStatuses = ["Active", "Đang ở"];
        private static readonly string[] BlockingHotelStatuses = ["Đã đặt", "Active", "Đang ở"];
        private static readonly string[] EditableCageStatuses = ["Trống", "Đang dọn dẹp", "Bảo trì", "Khóa"];
        private static readonly string[] MaintenanceCageStatuses = ["Đang dọn dẹp", "Bảo trì", "Khóa"];
        private const decimal MinimumRoomTypeDailyPrice = 150000m;
        private const decimal MinimumRoomTypeHourlyPrice = 40000m;
        private const decimal MaximumRoomTypePrice = 100000000m;
        private const int MaximumRoomTypeCapacity = 10;
        private const int MinimumCagePortionGrams = 10;
        private const int MaximumCagePortionGrams = 10000;

        private readonly PetStoreManagementContext _context;
        private readonly IHotelBookingHistoryService _historyService;
        private readonly IHotelCareMediaService _hotelCareMediaService;
        private readonly IHubContext<HotelCareHub> _hotelCareHub;
        private readonly IHotelCheckoutService _hotelCheckoutService;
        private readonly IHotelAvailabilityService _hotelAvailabilityService;
        private readonly IHotelReceptionService _hotelReceptionService;
        private readonly IInventoryBatchService _inventoryBatchService;
        private readonly IHotelEmailService _hotelEmailService;
        private readonly ILogger<SpaCagesController> _logger;

        // [nam] Khởi tạo controller vận hành chuồng và các dịch vụ Hotel liên quan.
        public SpaCagesController(
            PetStoreManagementContext context,
            IHotelBookingHistoryService historyService,
            IHotelCareMediaService hotelCareMediaService,
            IHubContext<HotelCareHub> hotelCareHub,
            IHotelCheckoutService hotelCheckoutService,
            IHotelAvailabilityService hotelAvailabilityService,
            IHotelReceptionService hotelReceptionService,
            IInventoryBatchService inventoryBatchService,
            IHotelEmailService hotelEmailService,
            ILogger<SpaCagesController> logger)
        {
            _context = context;
            _historyService = historyService;
            _hotelCareMediaService = hotelCareMediaService;
            _hotelCareHub = hotelCareHub;
            _hotelCheckoutService = hotelCheckoutService;
            _hotelAvailabilityService = hotelAvailabilityService;
            _hotelReceptionService = hotelReceptionService;
            _inventoryBatchService = inventoryBatchService;
            _hotelEmailService = hotelEmailService;
            _logger = logger;
        }

        // [nam] Lấy định danh và tên nhân viên đang thao tác để ghi lịch sử nghiệp vụ Hotel.
        private (int? UserId, string Name) GetCurrentStaffSnapshot()
        {
            int? userId = null;
            string? userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (int.TryParse(userIdValue, out int parsedUserId))
            {
                userId = parsedUserId;
            }

            string staffName = User.FindFirst("FullName")?.Value
                ?? User.Identity?.Name
                ?? "Nhân viên dịch vụ";

            return (userId, staffName);
        }

        // [nam] Chuẩn hóa trạng thái booking Hotel thành khóa dùng cho giao diện Staff.
        private static string ResolveHotelStatusKey(string? status)
        {
            return status?.Trim().ToLowerInvariant() switch
            {
                "đã đặt" => "reserved",
                "active" or "đang ở" => "active",
                "đã trả" => "completed",
                "đã hủy" or "cancelled" => "cancelled",
                _ => "other"
            };
        }
    }
}
