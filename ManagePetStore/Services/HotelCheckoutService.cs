using System.Data;
using ManagePetStore.Areas.ServiceStaff.Models;
using ManagePetStore.Models;
using Microsoft.EntityFrameworkCore;

namespace ManagePetStore.Services;

public class HotelCheckoutService : IHotelCheckoutService
{
    private readonly PetStoreManagementContext _context;

    public HotelCheckoutService(PetStoreManagementContext context) => _context = context;

    public async Task<HotelCheckoutPreviewViewModel?> GetPreviewAsync(int bookingId, DateTime? checkoutAt = null)
    {
        var booking = await LoadBookingAsync(bookingId, true);
        return booking == null ? null : BuildPreview(booking, checkoutAt ?? booking.CheckoutStatement?.CheckoutAt ?? DateTime.Now);
    }

    public async Task<HotelCheckoutPreviewViewModel> PrepareAsync(
        PrepareHotelCheckoutRequest request,
        int? staffUserId,
        string staffName)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var booking = await LoadBookingAsync(request.HotelBookingId, false)
            ?? throw new InvalidOperationException("Không tìm thấy booking Hotel.");

        if (booking.Status != "Active" && booking.Status != "Đang ở")
        {
            throw new InvalidOperationException("Chỉ có thể chốt chi phí cho pet đang lưu trú.");
        }

        if (booking.CheckoutStatement?.OrderId != null)
        {
            throw new InvalidOperationException("Bảng kê đã được thu ngân liên kết hóa đơn và không thể sửa.");
        }

        var checkoutAt = DateTime.Now;
        var preview = BuildPreview(booking, checkoutAt, request.OtherDescription, request.OtherAmount);
        var statement = booking.CheckoutStatement ?? new HotelCheckoutStatement
        {
            HotelBookingId = booking.HotelBookingId
        };

        if (booking.CheckoutStatement == null)
        {
            _context.HotelCheckoutStatements.Add(statement);
        }
        else
        {
            _context.HotelCheckoutItems.RemoveRange(statement.Items);
        }

        statement.Status = "ReadyForPayment";
        statement.CheckoutAt = checkoutAt;
        statement.RoomAmount = preview.RoomAmount;
        statement.FoodAmount = preview.FoodAmount;
        statement.AddonAmount = preview.AddonAmount;
        statement.LateFeeAmount = preview.LateFeeAmount;
        statement.OtherAmount = preview.OtherAmount;
        statement.DiscountAmount = preview.DiscountAmount;
        statement.TotalAmount = preview.TotalAmount;
        statement.PreparedByUserId = staffUserId;
        statement.PreparedByName = staffName;
        statement.PreparedAt = DateTime.Now;
        statement.Items = preview.Items.Select(item => new HotelCheckoutItem
        {
            ChargeType = item.ChargeType,
            Description = item.Description,
            Quantity = item.Quantity,
            Unit = item.Unit,
            UnitPrice = item.UnitPrice,
            Amount = item.Amount
        }).ToList();

        if (booking.FoodPlan != null)
        {
            booking.FoodPlan.ChargeableDays = ResolveFoodDays(booking, checkoutAt);
            booking.FoodPlan.TotalAmount = preview.FoodAmount - booking.FoodDiaryLogs.Where(log => log.IsExtraCharge).Sum(log => log.ExtraChargeAmount);
        }
        booking.FinalAmount = preview.TotalAmount;
        _context.PetBioTimelines.Add(new PetBioTimeline
        {
            PetId = booking.PetId,
            HotelBookingId = booking.HotelBookingId,
            Date = DateTime.Now,
            Title = "Gửi bảng kê tạm tính",
            Type = "HotelCheckoutPrepared",
            Description = $"{staffName} đã lập bảng kê tạm tính {preview.TotalAmount:N0}đ và gửi sang quầy thu ngân. Pet vẫn đang lưu trú cho đến khi hoàn tất trả pet."
        });

        var completedSpaIds = await _context.SpaBookings
            .Where(spa => spa.PetId == booking.PetId &&
                          spa.CustomerId == booking.CustomerId &&
                          (spa.SpaStatus == "4" || spa.SpaStatus.EndsWith("|4")) &&
                          (spa.Status == "Chờ thanh toán" || spa.Status == "pending" || spa.Status == "Chưa thanh toán") &&
                          spa.DateTime >= booking.CheckInDate && spa.DateTime <= checkoutAt)
            .Select(spa => spa.BookingId)
            .ToListAsync();
        var linkedSpaIds = await _context.HotelStaySpaLinks
            .Where(link => completedSpaIds.Contains(link.SpaBookingId))
            .Select(link => link.SpaBookingId)
            .ToListAsync();
        foreach (var spaId in completedSpaIds.Except(linkedSpaIds))
        {
            _context.HotelStaySpaLinks.Add(new HotelStaySpaLink
            {
                HotelBookingId = booking.HotelBookingId,
                SpaBookingId = spaId,
                LinkedAt = DateTime.Now
            });
        }

        await _context.SaveChangesAsync();
        await transaction.CommitAsync();
        return await GetPreviewAsync(booking.HotelBookingId) ?? preview;
    }

    private async Task<HotelBooking?> LoadBookingAsync(int bookingId, bool noTracking)
    {
        var query = _context.HotelBookings
            .Include(booking => booking.Pet)
            .Include(booking => booking.Cage).ThenInclude(cage => cage.RoomType)
            .Include(booking => booking.BookingAddons)
            .Include(booking => booking.FoodPlan)
            .Include(booking => booking.FoodDiaryLogs)
            .Include(booking => booking.CheckoutStatement).ThenInclude(statement => statement!.Items)
            .Include(booking => booking.CheckoutStatement).ThenInclude(statement => statement!.Order)
            .Where(booking => booking.HotelBookingId == bookingId);
        return await (noTracking ? query.AsNoTracking() : query).FirstOrDefaultAsync();
    }

    private static HotelCheckoutPreviewViewModel BuildPreview(
        HotelBooking booking,
        DateTime checkoutAt,
        string? otherDescription = null,
        decimal? requestedOtherAmount = null)
    {
        var foodDays = ResolveFoodDays(booking, checkoutAt);
        var planFoodAmount = (booking.FoodPlan?.PricePerDaySnapshot ?? 0) * foodDays;
        var extraFoodLogs = booking.FoodDiaryLogs.Where(log => log.IsExtraCharge && log.ExtraChargeAmount > 0).ToList();
        var extraFoodAmount = extraFoodLogs.Sum(log => log.ExtraChargeAmount);
        var addonAmount = booking.BookingAddons.Sum(addon => addon.Price);
        var lateFee = CalculateLateFee(booking, checkoutAt);
        var savedOtherItem = booking.CheckoutStatement?.Items.FirstOrDefault(item => item.ChargeType == "Other");
        var otherAmount = requestedOtherAmount ?? savedOtherItem?.Amount ?? 0;
        var description = string.IsNullOrWhiteSpace(otherDescription) ? savedOtherItem?.Description : otherDescription.Trim();
        var roomAmount = booking.Subtotal;
        var total = Math.Max(0, roomAmount - booking.Discount + planFoodAmount + extraFoodAmount + addonAmount + lateFee + otherAmount);

        var items = new List<HotelCheckoutPreviewItem>
        {
            new() { ChargeType = "Room", Description = $"Phòng {booking.Cage.RoomType.Type} · chuồng {booking.CageId}", Quantity = booking.StayDays, Unit = "ngày", UnitPrice = booking.BaseDailyPrice, Amount = roomAmount }
        };
        if (planFoodAmount > 0)
            items.Add(new() { ChargeType = "FoodPlan", Description = booking.FoodPlan!.FoodNameSnapshot, Quantity = foodDays, Unit = "ngày", UnitPrice = booking.FoodPlan.PricePerDaySnapshot, Amount = planFoodAmount });
        items.AddRange(booking.BookingAddons.Select(addon => new HotelCheckoutPreviewItem { ChargeType = "Addon", Description = addon.Name, Quantity = 1, Unit = "lần", UnitPrice = addon.Price, Amount = addon.Price }));
        items.AddRange(extraFoodLogs.Select(log => new HotelCheckoutPreviewItem { ChargeType = "ExtraFood", Description = $"Bữa phát sinh: {log.FoodType}", Quantity = 1, Unit = "lần", UnitPrice = log.ExtraChargeAmount, Amount = log.ExtraChargeAmount }));
        if (lateFee > 0) items.Add(new() { ChargeType = "LateFee", Description = "Phụ phí checkout trễ", Quantity = 1, Unit = "lần", UnitPrice = lateFee, Amount = lateFee });
        if (otherAmount > 0) items.Add(new() { ChargeType = "Other", Description = description ?? "Chi phí phát sinh khác", Quantity = 1, Unit = "lần", UnitPrice = otherAmount, Amount = otherAmount });

        var orderStatus = booking.CheckoutStatement?.Order?.Status;
        return new HotelCheckoutPreviewViewModel
        {
            HotelBookingId = booking.HotelBookingId,
            PetName = booking.Pet.Name,
            CageId = booking.CageId,
            CheckoutAt = checkoutAt,
            RoomAmount = roomAmount,
            FoodAmount = planFoodAmount + extraFoodAmount,
            AddonAmount = addonAmount,
            LateFeeAmount = lateFee,
            OtherAmount = otherAmount,
            DiscountAmount = booking.Discount,
            TotalAmount = total,
            StatementStatus = booking.CheckoutStatement?.Status ?? "Draft",
            OrderId = booking.CheckoutStatement?.OrderId,
            OrderStatus = orderStatus,
            CanFinalize = booking.CheckoutStatement?.OrderId != null && (orderStatus == "Chờ xử lý" || orderStatus == "Đã thanh toán"),
            Items = items
        };
    }

    private static int ResolveFoodDays(HotelBooking booking, DateTime checkoutAt)
    {
        if (booking.FoodPlan == null || booking.FoodPlan.PricePerDaySnapshot <= 0) return 0;
        var start = booking.ActualCheckInAt ?? booking.CheckInDate;
        var actualDays = Math.Max(1, (int)Math.Ceiling(Math.Max(0, (checkoutAt - start).TotalHours) / 24d));
        return actualDays;
    }

    private static decimal CalculateLateFee(HotelBooking booking, DateTime checkoutAt)
    {
        var scheduled = booking.ScheduledCheckOutDate ?? booking.CheckOutDate;
        if (scheduled.HasValue && scheduled.Value.TimeOfDay == TimeSpan.Zero)
            scheduled = scheduled.Value.Date.AddHours(12);
        if (!scheduled.HasValue || checkoutAt <= scheduled.Value.AddMinutes(30)) return 0;
        var hours = (int)Math.Ceiling((checkoutAt - scheduled.Value.AddMinutes(30)).TotalHours);
        var fullDays = hours / 24;
        var remainingHours = hours % 24;
        return fullDays * booking.BaseDailyPrice + Math.Min(remainingHours * booking.Cage.RoomType.HourlyPrice, booking.BaseDailyPrice);
    }
}
