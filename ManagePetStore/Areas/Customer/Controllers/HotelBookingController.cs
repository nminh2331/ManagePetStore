using System.Data;
using System.Security.Claims;
using ManagePetStore.Areas.Customer.Models;
using ManagePetStore.Models;
using ManagePetStore.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ManagePetStore.Areas.Customer.Controllers;

[Area("Customer")]
[Authorize]
public class HotelBookingController : Controller
{
    private static readonly string[] BlockingStatuses = ["Đã đặt", "Active", "Đang ở"];

    private readonly PetStoreManagementContext _context;
    private readonly IHotelBookingHistoryService _historyService;
    private readonly ILogger<HotelBookingController> _logger;

    public HotelBookingController(
        PetStoreManagementContext context,
        IHotelBookingHistoryService historyService,
        ILogger<HotelBookingController> logger)
    {
        _context = context;
        _historyService = historyService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? searchTerm, string statusFilter = "all", int page = 1)
    {
        var layout = await BuildSidebarViewModelAsync("appointments");
        if (layout == null)
        {
            return RedirectToAction("Login", "Account", new { area = "Customer" });
        }

        // Tải lịch đặt khách sạn
        var bookings = await _context.HotelBookings
            .AsNoTracking()
            .Include(b => b.Pet)
            .Include(b => b.Cage)
                .ThenInclude(c => c.RoomType)
            .Where(b => b.CustomerId == layout.Customer.CustomerId)
            .OrderByDescending(b => b.CheckInDate)
            .ThenByDescending(b => b.HotelBookingId)
            .ToListAsync();

        var mappedBookings = bookings.Select(MapToListItem).ToList();
        var normalizedSearch = searchTerm?.Trim() ?? "";
        var normalizedStatus = string.IsNullOrWhiteSpace(statusFilter)
            ? "all"
            : statusFilter.Trim().ToLowerInvariant();

        IEnumerable<HotelBookingListItemViewModel> filteredBookings = mappedBookings;

        if (!string.IsNullOrWhiteSpace(normalizedSearch))
        {
            filteredBookings = filteredBookings.Where(b =>
                b.DisplayBookingId.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase) ||
                b.PetName.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase) ||
                b.CageId.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase) ||
                b.RoomTypeName.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase) ||
                b.Status.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase));
        }

        filteredBookings = normalizedStatus switch
        {
            "reserved" => filteredBookings.Where(b => b.StatusKey == "reserved"),
            "active" => filteredBookings.Where(b => b.StatusKey == "active"),
            "completed" => filteredBookings.Where(b => b.StatusKey == "completed"),
            "cancelled" => filteredBookings.Where(b => b.StatusKey == "cancelled"),
            _ => filteredBookings
        };

        var filteredBookingList = filteredBookings.ToList();
        var currentPage = page < 1 ? 1 : page;
        var pageSize = new HotelBookingHistoryPageViewModel().PageSize;
        var totalFilteredItems = filteredBookingList.Count;
        var totalPages = totalFilteredItems == 0 ? 0 : (int)Math.Ceiling(totalFilteredItems / (double)pageSize);

        if (totalPages > 0 && currentPage > totalPages)
        {
            currentPage = totalPages;
        }

        var model = new HotelBookingHistoryPageViewModel
        {
            User = layout.User,
            Customer = layout.Customer,
            ActiveNav = layout.ActiveNav,
            Bookings = mappedBookings,
            SearchTerm = normalizedSearch,
            StatusFilter = normalizedStatus,
            Page = totalPages == 0 ? 1 : currentPage,
            TotalFilteredItems = totalFilteredItems,
            TotalPages = totalPages
        };

        model.VisibleBookings = filteredBookingList
            .Skip((model.Page - 1) * model.PageSize)
            .Take(model.PageSize)
            .ToList();

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var layout = await BuildSidebarViewModelAsync("appointments");
        if (layout == null)
        {
            return RedirectToAction("Login", "Account", new { area = "Customer" });
        }

        var booking = await _historyService.GetDetailAsync(id, layout.Customer.CustomerId);
        if (booking == null)
        {
            return NotFound();
        }

        return View(new HotelBookingDetailPageViewModel
        {
            User = layout.User,
            Customer = layout.Customer,
            ActiveNav = layout.ActiveNav,
            Booking = booking
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Book([FromForm] HotelBookingRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BookingError(GetModelStateErrorMessage());
        }

        var customer = await GetCurrentCustomerAsync();
        if (customer == null)
        {
            return RedirectToAction("Login", "Account", new { area = "Customer" });
        }

        var petId = request.PetId!.Value;
        var roomTypeId = request.RoomTypeId!.Value;
        var checkIn = request.CheckInDate!.Value.Date;
        var checkOut = request.CheckOutDate!.Value.Date;
        var stayDays = (checkOut - checkIn).Days;

        await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        try
        {
            var pet = await _context.Pets
                .FirstOrDefaultAsync(p => p.PetId == petId && p.CustomerId == customer.CustomerId);

            if (pet == null)
            {
                return BookingError("Không tìm thấy thú cưng hoặc thú cưng không thuộc tài khoản của bạn.");
            }

            if (!string.Equals(pet.Status, "Active", StringComparison.OrdinalIgnoreCase))
            {
                return BookingError("Hồ sơ thú cưng đã chọn không còn hoạt động.");
            }

            var roomType = await _context.RoomTypes
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.RoomTypeId == roomTypeId && r.Status);

            if (roomType == null)
            {
                return BookingError("Loại phòng đã chọn hiện không còn hoạt động.");
            }

            var petHasConflict = await _context.HotelBookings.AnyAsync(b =>
                b.PetId == petId &&
                BlockingStatuses.Contains(b.Status) &&
                b.CheckInDate < checkOut &&
                (!b.CheckOutDate.HasValue || b.CheckOutDate.Value > checkIn));

            if (petHasConflict)
            {
                return BookingError($"{pet.Name} đã có lịch lưu trú trùng với khoảng ngày bạn chọn.");
            }

            var conflictingCageIds = await _context.HotelBookings
                .Where(b =>
                    BlockingStatuses.Contains(b.Status) &&
                    b.CheckInDate < checkOut &&
                    (!b.CheckOutDate.HasValue || b.CheckOutDate.Value > checkIn))
                .Select(b => b.CageId)
                .Distinct()
                .ToListAsync();

            var cage = await _context.Cages
                .AsNoTracking()
                .Where(c =>
                    c.RoomTypeId == roomTypeId &&
                    c.Status == "Trống" &&
                    !conflictingCageIds.Contains(c.CageId))
                .OrderBy(c => c.CageId)
                .FirstOrDefaultAsync();

            if (cage == null)
            {
                return BookingError("Loại phòng này đã hết chuồng trống trong khoảng ngày bạn chọn.");
            }

            var subtotal = roomType.DailyPrice * stayDays;
            var discountRate = ResolveDiscountRate(customer.MembershipTier);
            var discount = decimal.Round(subtotal * discountRate, 0, MidpointRounding.AwayFromZero);
            var finalAmount = subtotal - discount;

            var booking = new HotelBooking
            {
                CageId = cage.CageId,
                PetId = pet.PetId,
                CustomerId = customer.CustomerId,
                CheckInDate = checkIn,
                CheckOutDate = checkOut,
                ScheduledCheckInDate = checkIn,
                ScheduledCheckOutDate = checkOut,
                StayDays = stayDays,
                BaseDailyPrice = roomType.DailyPrice,
                Subtotal = subtotal,
                Discount = discount,
                FinalAmount = finalAmount,
                EarnedPoints = 0,
                Status = "Đã đặt"
            };

            _context.HotelBookings.Add(booking);
            _context.PetBioTimelines.Add(new PetBioTimeline
            {
                PetId = pet.PetId,
                HotelBooking = booking,
                Date = DateTime.Now,
                Title = "Đặt phòng Hotel",
                Type = "HotelBookingCreated",
                Description = $"Khách hàng đặt chuồng {cage.CageId} từ {checkIn:dd/MM/yyyy} đến {checkOut:dd/MM/yyyy}."
            });

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            TempData["SuccessMessage"] =
                $"Đặt phòng thành công cho {pet.Name}. Chuồng dự kiến: {cage.CageId}.";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(
                ex,
                "Không thể đặt Hotel online cho CustomerId {CustomerId}, PetId {PetId}.",
                customer.CustomerId,
                petId);
            return BookingError("Không thể hoàn tất đặt phòng do lỗi hệ thống. Vui lòng thử lại.");
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id, string? searchTerm, string statusFilter = "all", int page = 1)
    {
        var customer = await GetCurrentCustomerAsync();
        if (customer == null)
        {
            return RedirectToAction("Login", "Account", new { area = "Customer" });
        }

        var booking = await _context.HotelBookings
            .Include(b => b.Pet)
            .FirstOrDefaultAsync(b =>
                b.HotelBookingId == id &&
                b.CustomerId == customer.CustomerId);

        if (booking == null)
        {
            TempData["ErrorMessage"] = "Không tìm thấy lịch đặt phòng hoặc bạn không có quyền hủy.";
            return RedirectToAction(nameof(Index), new { searchTerm, statusFilter, page });
        }

        if (!string.Equals(booking.Status, "Đã đặt", StringComparison.OrdinalIgnoreCase))
        {
            TempData["ErrorMessage"] = "Chỉ có thể hủy lịch đang ở trạng thái Đã đặt.";
            return RedirectToAction(nameof(Index), new { searchTerm, statusFilter, page });
        }

        if (booking.CheckInDate.Date <= DateTime.Today)
        {
            TempData["ErrorMessage"] = "Không thể hủy online vào hoặc sau ngày nhận phòng. Vui lòng liên hệ cửa hàng.";
            return RedirectToAction(nameof(Index), new { searchTerm, statusFilter, page });
        }

        booking.Status = "Đã hủy";
        _context.PetBioTimelines.Add(new PetBioTimeline
        {
            PetId = booking.PetId,
            HotelBookingId = booking.HotelBookingId,
            Date = DateTime.Now,
            Title = "Hủy lịch lưu trú",
            Type = "HotelBookingCancelled",
            Description = "Khách hàng đã hủy lịch đặt phòng qua hệ thống."
        });
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Đã hủy lịch Hotel của {booking.Pet.Name}.";
        return RedirectToAction(nameof(Index), new { searchTerm, statusFilter, page });
    }

    private IActionResult BookingError(string message)
    {
        TempData["HotelError"] = message;
        return RedirectToAction("Index", "Home", new { area = "", hotel = "book" });
    }

    private async Task<ManagePetStore.Models.Customer?> GetCurrentCustomerAsync()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out var userId))
        {
            return null;
        }

        return await _context.Customers.FirstOrDefaultAsync(c => c.UserId == userId);
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

    private string GetModelStateErrorMessage()
    {
        return ModelState.Values
            .SelectMany(v => v.Errors)
            .Select(e => e.ErrorMessage)
            .FirstOrDefault(message => !string.IsNullOrWhiteSpace(message))
            ?? "Thông tin đặt phòng không hợp lệ.";
    }

    private static decimal ResolveDiscountRate(string? membershipTier)
    {
        return membershipTier?.Trim().ToLowerInvariant() switch
        {
            "gold" or "vàng" => 0.10m,
            "silver" or "bạc" => 0.05m,
            _ => 0m
        };
    }

    private static HotelBookingListItemViewModel MapToListItem(HotelBooking booking)
    {
        var statusKey = ResolveStatusKey(booking.Status);

        return new HotelBookingListItemViewModel
        {
            HotelBookingId = booking.HotelBookingId,
            PetName = booking.Pet.Name,
            CageId = booking.CageId,
            RoomTypeName = booking.Cage.RoomType.Type,
            CheckInDate = booking.ScheduledCheckInDate ?? booking.CheckInDate,
            CheckOutDate = booking.ScheduledCheckOutDate
                ?? booking.CheckOutDate
                ?? booking.CheckInDate.AddDays(booking.StayDays),
            StayDays = booking.StayDays,
            FinalAmount = booking.FinalAmount,
            Status = booking.Status,
            StatusKey = statusKey,
            CanCancel = statusKey == "reserved" &&
                (booking.ScheduledCheckInDate ?? booking.CheckInDate).Date > DateTime.Today
        };
    }

    private static string ResolveStatusKey(string? status)
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
