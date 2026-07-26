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
    [HttpPost]
    [ValidateAntiForgeryToken]
    // [nam] Tạo yêu cầu đổi chuồng sau khi kiểm tra quyền sở hữu, trùng lịch và chênh lệch giá.
    public async Task<IActionResult> RequestCageChange(int id, string targetCageId, string reason)
    {
        var customer = await GetCurrentCustomerAsync();
        if (customer == null)
        {
            return RedirectToAction("Login", "Account", new { area = "Customer" });
        }

        targetCageId = targetCageId?.Trim().ToUpperInvariant() ?? string.Empty;
        reason = reason?.Trim() ?? string.Empty;
        if (targetCageId.Length is < 1 or > 20 || reason.Length is < 10 or > 500)
        {
            TempData["ErrorMessage"] = "Vui lòng chọn chuồng đích và nhập lý do từ 10 đến 500 ký tự.";
            return RedirectToAction(nameof(Details), new { id });
        }

        await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        try
        {
            var booking = await _context.HotelBookings
                .Include(item => item.Cage).ThenInclude(cage => cage.RoomType)
                .Include(item => item.CheckoutStatement)
                .FirstOrDefaultAsync(item => item.HotelBookingId == id && item.CustomerId == customer.CustomerId);
            if (booking == null || ResolveStatusKey(booking.Status) is not ("reserved" or "active"))
            {
                TempData["ErrorMessage"] = "Booking không còn đủ điều kiện gửi yêu cầu đổi chuồng.";
                return RedirectToAction(nameof(Details), new { id });
            }

            if (booking.CheckoutStatement != null)
            {
                TempData["ErrorMessage"] = "Chi phí booking đã được chốt, không thể gửi thêm yêu cầu đổi chuồng.";
                return RedirectToAction(nameof(Details), new { id });
            }

            if (string.Equals(booking.CageId, targetCageId, StringComparison.OrdinalIgnoreCase))
            {
                TempData["ErrorMessage"] = "Pet đang được xếp tại chuồng này.";
                return RedirectToAction(nameof(Details), new { id });
            }

            if (await _context.HotelCageChangeRequests.AnyAsync(item =>
                    item.HotelBookingId == id && item.Status == "Pending"))
            {
                TempData["ErrorMessage"] = "Booking đang có một yêu cầu đổi chuồng chờ xử lý.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var targetCage = await _context.Cages
                .Include(cage => cage.RoomType)
                .FirstOrDefaultAsync(cage => cage.CageId == targetCageId &&
                                             cage.Status == "Trống" &&
                                             cage.RoomType.Status &&
                                             HotelRoomTypeCatalog.Codes.Contains(cage.RoomType.Code));
            if (targetCage == null || await HasCageConflictAsync(booking, targetCageId))
            {
                TempData["ErrorMessage"] = "Chuồng đích không còn khả dụng trong thời gian lưu trú.";
                return RedirectToAction(nameof(Details), new { id });
            }

            int remainingDays = ResolveRemainingChargeDays(booking);
            decimal discountRate = ResolveDiscountRate(customer.MembershipTier);
            decimal estimatedDifference = decimal.Round(
                (targetCage.RoomType.DailyPrice - booking.BaseDailyPrice) * remainingDays * (1 - discountRate),
                0,
                MidpointRounding.AwayFromZero);

            _context.HotelCageChangeRequests.Add(new HotelCageChangeRequest
            {
                HotelBookingId = booking.HotelBookingId,
                CustomerId = customer.CustomerId,
                SourceCageId = booking.CageId,
                TargetCageId = targetCage.CageId,
                Reason = reason,
                Status = "Pending",
                RemainingDaysSnapshot = remainingDays,
                SourceDailyPriceSnapshot = booking.BaseDailyPrice,
                TargetDailyPriceSnapshot = targetCage.RoomType.DailyPrice,
                PriceDifferenceSnapshot = estimatedDifference,
                RequestedAt = DateTime.Now
            });
            _context.PetBioTimelines.Add(new PetBioTimeline
            {
                PetId = booking.PetId,
                HotelBookingId = booking.HotelBookingId,
                Date = DateTime.Now,
                Title = "Yêu cầu đổi chuồng",
                Type = "CageChangeRequested",
                Description = $"Khách hàng yêu cầu đổi từ chuồng {booking.CageId} sang {targetCage.CageId}. Lý do: {reason}. Chênh lệch dự kiến: {estimatedDifference:N0}đ."
            });

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            TempData["SuccessMessage"] = $"Đã gửi yêu cầu đổi sang chuồng {targetCage.CageId}. Nhân viên sẽ kiểm tra và phản hồi.";
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Cannot create cage change request for HotelBooking {BookingId}.", id);
            TempData["ErrorMessage"] = "Không thể gửi yêu cầu đổi chuồng lúc này.";
        }

        return RedirectToAction(nameof(Details), new { id });
    }


    // [nam] Tính các chuồng có thể chuyển đến và chênh lệch chi phí ước tính.
    private async Task<List<HotelCageChangeOptionViewModel>> GetAvailableCageChangeOptionsAsync(
        HotelBookingHistoryDetailViewModel booking,
        string membershipTier)
    {
        var intervalStart = booking.StatusKey == "active" ? DateTime.Now : booking.CheckInDate;
        var intervalEnd = booking.ScheduledCheckOutDate
            ?? booking.CheckOutDate
            ?? booking.CheckInDate.AddDays(Math.Max(booking.StayDays, 1));
        var conflictingCageIds = await _context.HotelBookings
            .AsNoTracking()
            .Where(item => item.HotelBookingId != booking.HotelBookingId &&
                           BlockingStatuses.Contains(item.Status) &&
                           item.CheckInDate < intervalEnd &&
                           (!item.CheckOutDate.HasValue || item.CheckOutDate.Value > intervalStart))
            .Select(item => item.CageId)
            .Distinct()
            .ToListAsync();
        int remainingDays = booking.StatusKey == "reserved"
            ? Math.Max(booking.StayDays, 1)
            : Math.Max(1, (int)Math.Ceiling((intervalEnd - DateTime.Now).TotalHours / 24d));
        decimal discountRate = ResolveDiscountRate(membershipTier);

        var cages = await _context.Cages
            .AsNoTracking()
            .Where(cage => cage.CageId != booking.CageId &&
                           cage.Status == "Trống" &&
                           cage.RoomType.Status &&
                           HotelRoomTypeCatalog.Codes.Contains(cage.RoomType.Code) &&
                           !conflictingCageIds.Contains(cage.CageId))
            .OrderBy(cage => cage.RoomType.DailyPrice)
            .ThenBy(cage => cage.CageId)
            .Select(cage => new
            {
                CageId = cage.CageId,
                RoomTypeName = cage.RoomType.Type,
                RoomTypeCode = cage.RoomType.Code,
                Size = cage.RoomType.Size,
                DailyPrice = cage.RoomType.DailyPrice
            })
            .ToListAsync();

        return cages.Select(cage => new HotelCageChangeOptionViewModel
        {
            CageId = cage.CageId,
            RoomTypeName = cage.RoomTypeName,
            RoomTypeCode = cage.RoomTypeCode,
            Size = cage.Size,
            DailyPrice = cage.DailyPrice,
            EstimatedPriceDifference = decimal.Round(
                (cage.DailyPrice - booking.BaseDailyPrice) * remainingDays * (1 - discountRate),
                0,
                MidpointRounding.AwayFromZero)
        }).ToList();
    }

    // [nam] Kiểm tra chuồng đích có booking khác trùng khoảng thời gian hay không.
    private async Task<bool> HasCageConflictAsync(HotelBooking booking, string targetCageId)
    {
        var intervalStart = ResolveStatusKey(booking.Status) == "active" ? DateTime.Now : booking.CheckInDate;
        var intervalEnd = booking.ScheduledCheckOutDate
            ?? booking.CheckOutDate
            ?? booking.CheckInDate.AddDays(Math.Max(booking.StayDays, 1));
        return await _availabilityService.HasCageConflictAsync(
            targetCageId,
            intervalStart,
            intervalEnd,
            booking.HotelBookingId);
    }

    // [nam] Tính số ngày còn lại dùng để ước tính chênh lệch giá đổi chuồng.
    private static int ResolveRemainingChargeDays(HotelBooking booking)
    {
        if (ResolveStatusKey(booking.Status) == "reserved")
        {
            return Math.Max(booking.StayDays, 1);
        }

        var expectedCheckout = booking.ScheduledCheckOutDate
            ?? booking.CheckOutDate
            ?? DateTime.Now.AddDays(1);
        return Math.Max(1, (int)Math.Ceiling((expectedCheckout - DateTime.Now).TotalHours / 24d));
    }

}
