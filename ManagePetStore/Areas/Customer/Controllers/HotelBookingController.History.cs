using System.Data;
using System.Security.Claims;
using ManagePetStore.Areas.Customer.Models;
using ManagePetStore.Models;
using ManagePetStore.Services;
using ManagePetStore.Services.Warehouse;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ManagePetStore.Areas.Customer.Controllers;

public partial class HotelBookingController
{
    [HttpGet]
    // [nam] Hiển thị, tìm kiếm, lọc và phân trang lịch sử đặt chuồng của khách hàng.
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
            .OrderByDescending(b => b.HotelBookingId)
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
    // [nam] Tải chi tiết booking cùng trạng thái và các lựa chọn đổi chuồng khả dụng.
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

        var pendingRequest = await _context.HotelCageChangeRequests
            .AsNoTracking()
            .Where(request => request.HotelBookingId == id && request.Status == "Pending")
            .OrderByDescending(request => request.RequestedAt)
            .Select(request => new HotelCageChangeRequestItemViewModel
            {
                ChangeRequestId = request.ChangeRequestId,
                SourceCageId = request.SourceCageId,
                TargetCageId = request.TargetCageId,
                Reason = request.Reason,
                Status = request.Status,
                EstimatedPriceDifference = request.PriceDifferenceSnapshot,
                RequestedAt = request.RequestedAt
            })
            .FirstOrDefaultAsync();

        bool canRequestCageChange = booking.StatusKey is "reserved" or "active";
        List<HotelCageChangeOptionViewModel> availableCages = canRequestCageChange && pendingRequest == null
            ? await GetAvailableCageChangeOptionsAsync(booking)
            : [];

        return View(new HotelBookingDetailPageViewModel
        {
            User = layout.User,
            Customer = layout.Customer,
            ActiveNav = layout.ActiveNav,
            Booking = booking,
            CanRequestCageChange = canRequestCageChange,
            PendingCageChangeRequest = pendingRequest,
            AvailableCages = availableCages
        });
    }


    // [nam] Chuyển entity booking thành dữ liệu hiển thị trên danh sách của khách hàng.
    private static HotelBookingListItemViewModel MapToListItem(HotelBooking booking)
    {
        var statusKey = ResolveStatusKey(booking.Status);

        bool canCancel = statusKey == "reserved" &&
            (booking.ScheduledCheckInDate ?? booking.CheckInDate) > DateTime.Now.AddHours(1);

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
            CanCancel = canCancel,
            ShowCannotCancelOnline = statusKey == "reserved" && !canCancel
        };
    }

}
