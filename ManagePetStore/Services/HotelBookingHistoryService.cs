using ManagePetStore.Models;
using Microsoft.EntityFrameworkCore;

namespace ManagePetStore.Services;

public class HotelBookingHistoryService : IHotelBookingHistoryService
{
    private static readonly string[] LegacyHotelTimelineTypes =
    [
        "HotelBookingCreated",
        "HotelBookingCancelled",
        "HealthCheckIn",
        "PetCheckIn",
        "HotelCageMove",
        "HotelCheckOut"
    ];

    private readonly PetStoreManagementContext _context;

    public HotelBookingHistoryService(PetStoreManagementContext context)
    {
        _context = context;
    }

    public async Task<HotelBookingHistoryDetailViewModel?> GetDetailAsync(
        int hotelBookingId,
        int? customerId = null)
    {
        var bookingQuery = _context.HotelBookings
            .AsNoTracking()
            .Include(booking => booking.Pet)
            .Include(booking => booking.Customer)
            .Include(booking => booking.Cage)
                .ThenInclude(cage => cage.RoomType)
            .Include(booking => booking.BookingAddons)
            .Include(booking => booking.FoodPlan)
            .Where(booking => booking.HotelBookingId == hotelBookingId);

        if (customerId.HasValue)
        {
            bookingQuery = bookingQuery.Where(booking => booking.CustomerId == customerId.Value);
        }

        var booking = await bookingQuery.FirstOrDefaultAsync();
        if (booking == null)
        {
            return null;
        }

        var plannedEnd = booking.ScheduledCheckOutDate
            ?? booking.CheckOutDate
            ?? booking.CheckInDate.AddDays(Math.Max(booking.StayDays, 1));
        var legacyRangeStart = (booking.ScheduledCheckInDate ?? booking.CheckInDate).AddHours(-12);
        var legacyRangeEnd = plannedEnd.AddHours(12);

        var timeline = await _context.PetBioTimelines
            .AsNoTracking()
            .Where(item =>
                item.HotelBookingId == booking.HotelBookingId ||
                (item.HotelBookingId == null &&
                 item.PetId == booking.PetId &&
                 LegacyHotelTimelineTypes.Contains(item.Type) &&
                 item.Date >= legacyRangeStart &&
                 item.Date <= legacyRangeEnd))
            .OrderByDescending(item => item.Date)
            .ThenByDescending(item => item.TimelineId)
            .Select(item => new HotelBookingTimelineHistoryItem
            {
                OccurredAt = item.Date,
                Type = item.Type,
                Title = item.Title,
                Description = item.Description
            })
            .ToListAsync();

        var medicalRecords = await _context.MedicalRecords
            .AsNoTracking()
            .Where(record =>
                record.HotelBookingId == booking.HotelBookingId ||
                (record.HotelBookingId == null &&
                 record.PetId == booking.PetId &&
                 record.DateCreated >= legacyRangeStart &&
                 record.DateCreated <= legacyRangeEnd))
            .OrderByDescending(record => record.DateCreated)
            .Select(record => new HotelBookingMedicalHistoryItem
            {
                RecordId = record.RecordId,
                DateCreated = record.DateCreated,
                Weight = record.Weight,
                HealthStatus = record.HealthStatus,
                Symptoms = record.Symptoms,
                Treatment = record.Treatment,
                VaccinationStatus = record.VaccinationStatus,
                ParasitePrevention = record.ParasitePrevention,
                PhysicalCheck = record.PhysicalCheck
            })
            .ToListAsync();

        var careLogs = await _context.FoodDiaryLogs
            .AsNoTracking()
            .Where(log =>
                (log.HotelBookingId == booking.HotelBookingId &&
                 (!customerId.HasValue || log.IsVisibleToCustomer)) ||
                (log.HotelBookingId == null &&
                 log.OccurredAt.HasValue &&
                 log.CageId == booking.CageId &&
                 log.PetName == booking.Pet.Name &&
                 log.OccurredAt.Value >= legacyRangeStart &&
                 log.OccurredAt.Value <= legacyRangeEnd))
            .OrderByDescending(log => log.OccurredAt)
            .ThenByDescending(log => log.LogId)
            .Select(log => new HotelBookingCareHistoryItem
            {
                LogId = log.LogId,
                OccurredAt = log.OccurredAt,
                LegacyTime = log.Time,
                Status = log.Status,
                ActivityType = log.ActivityType,
                Title = log.Title,
                FoodType = log.FoodType,
                Amount = log.Amount,
                PhotoUrl = log.PhotoUrl,
                MediaUrl = log.MediaUrl,
                MediaType = log.MediaType,
                MealType = log.MealType,
                ServedGrams = log.ServedGrams,
                ConsumedPercent = log.ConsumedPercent,
                IsExtraCharge = log.IsExtraCharge,
                ExtraChargeAmount = log.ExtraChargeAmount,
                Note = log.Note,
                StaffName = log.StaffName
            })
            .ToListAsync();

        return new HotelBookingHistoryDetailViewModel
        {
            HotelBookingId = booking.HotelBookingId,
            Status = booking.Status,
            StatusKey = ResolveStatusKey(booking.Status),
            CheckInDate = booking.CheckInDate,
            CheckOutDate = booking.CheckOutDate,
            ScheduledCheckInDate = booking.ScheduledCheckInDate,
            ScheduledCheckOutDate = booking.ScheduledCheckOutDate,
            ActualCheckInAt = booking.ActualCheckInAt,
            ActualCheckOutAt = booking.ActualCheckOutAt,
            StayDays = booking.StayDays,
            BaseDailyPrice = booking.BaseDailyPrice,
            Subtotal = booking.Subtotal,
            Discount = booking.Discount,
            FinalAmount = booking.FinalAmount,
            EarnedPoints = booking.EarnedPoints,
            PetId = booking.PetId,
            PetName = booking.Pet.Name,
            PetSpecies = booking.Pet.Species,
            PetBreed = booking.Pet.Breed,
            PetAge = booking.Pet.Age,
            PetWeight = booking.Pet.Weight,
            PetPathology = booking.Pet.Pathology,
            PetImageUrl = booking.Pet.ImageUrl,
            CustomerName = booking.Customer.FullName,
            CustomerPhone = booking.Customer.Phone,
            CustomerEmail = booking.Customer.Email,
            CageId = booking.CageId,
            RoomTypeName = booking.Cage.RoomType.Type,
            RoomSize = booking.Cage.RoomType.Size,
            HasAc = booking.Cage.RoomType.HasAc,
            HasCamera = booking.Cage.RoomType.HasCamera,
            HasPremiumFood = booking.Cage.RoomType.HasPremiumFood,
            FoodPlanName = booking.FoodPlan?.FoodNameSnapshot ?? "Chủ nuôi tự chuẩn bị",
            FoodPricePerDay = booking.FoodPlan?.PricePerDaySnapshot ?? 0,
            FoodPortionGrams = booking.FoodPlan?.PortionGrams ?? 0,
            FoodMealsPerDay = booking.FoodPlan?.MealsPerDay ?? 0,
            FeedingInstructions = booking.FoodPlan?.FeedingInstructions,
            FoodAllergyNotes = booking.FoodPlan?.AllergyNotes,
            Addons = booking.BookingAddons
                .OrderBy(addon => addon.AddonId)
                .Select(addon => new HotelBookingAddonHistoryItem
                {
                    Name = addon.Name,
                    Price = addon.Price
                })
                .ToList(),
            Timeline = timeline,
            MedicalRecords = medicalRecords,
            CareLogs = careLogs
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
