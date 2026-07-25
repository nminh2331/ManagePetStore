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
using ManagePetStore.Services.Warehouse;
using CustomerEntity = ManagePetStore.Models.Customer;

namespace ManagePetStore.Areas.ServiceStaff.Controllers
{
    public partial class SpaCagesController
    {
        [HttpGet("GetCageMapDetail")]
        public async Task<IActionResult> GetCageMapDetail(string cageId)
        {
            if (string.IsNullOrWhiteSpace(cageId))
            {
                return Json(new { success = false, message = "Mã chuồng không hợp lệ." });
            }

            cageId = cageId.Trim().ToUpperInvariant();
            var cage = await _context.Cages
                .AsNoTracking()
                .Include(c => c.RoomType)
                .FirstOrDefaultAsync(c => c.CageId == cageId);

            if (cage == null)
            {
                return Json(new { success = false, message = "Không tìm thấy chuồng." });
            }

            var booking = await _context.HotelBookings
                .AsNoTracking()
                .Include(b => b.Pet)
                .Include(b => b.Customer)
                .Include(b => b.BookingAddons)
                .Where(b => b.CageId == cageId && ActiveHotelStatuses.Contains(b.Status))
                .OrderByDescending(b => b.CheckInDate)
                .FirstOrDefaultAsync();

            var careLogs = await _context.FoodDiaryLogs
                .AsNoTracking()
                .Where(log => log.CageId == cageId)
                .OrderByDescending(log => log.Time)
                .Take(5)
                .Select(log => new
                {
                    log.Status,
                    log.FoodType,
                    log.Amount,
                    log.Note,
                    log.Time,
                    log.StaffName
                })
                .ToListAsync();

            var maintenanceHistory = await _context.RoomMaintenanceLogs
                .AsNoTracking()
                .Where(log => log.CageId == cageId)
                .OrderByDescending(log => log.StartedAt)
                .Take(8)
                .Select(log => new
                {
                    log.MaintenanceLogId,
                    log.PreviousStatus,
                    log.NewStatus,
                    log.Reason,
                    log.Note,
                    log.StartedAt,
                    log.EndedAt,
                    log.CreatedByName,
                    log.EndedByName,
                    IsOpen = log.EndedAt == null
                })
                .ToListAsync();

            var availableDestinations = new List<object>();
            if (booking != null)
            {
                var conflictingCageIds = await _context.HotelBookings
                    .AsNoTracking()
                    .Where(b =>
                        b.HotelBookingId != booking.HotelBookingId &&
                        BlockingHotelStatuses.Contains(b.Status) &&
                        (!booking.CheckOutDate.HasValue || b.CheckInDate < booking.CheckOutDate.Value) &&
                        (!b.CheckOutDate.HasValue || b.CheckOutDate.Value > booking.CheckInDate))
                    .Select(b => b.CageId)
                    .Distinct()
                    .ToListAsync();

                var destinationCages = await _context.Cages
                    .AsNoTracking()
                    .Where(c =>
                        c.CageId != cageId &&
                        c.Status == "Trống" &&
                        c.RoomType.Status &&
                        HotelRoomTypeCatalog.Codes.Contains(c.RoomType.Code) &&
                        !conflictingCageIds.Contains(c.CageId))
                    .OrderBy(c => c.CageId)
                    .Select(c => new
                    {
                        c.CageId,
                        RoomType = c.RoomType.Type,
                        c.RoomType.Size
                    })
                    .ToListAsync();
                availableDestinations = destinationCages.Cast<object>().ToList();
            }

            return Json(new
            {
                success = true,
                cage = new
                {
                    cage.CageId,
                    cage.Status,
                    cage.ImageUrl,
                    cage.FeedSchedule,
                    cage.Portion,
                    roomType = new
                    {
                        cage.RoomType.RoomTypeId,
                        cage.RoomType.Type,
                        cage.RoomType.Size,
                        cage.RoomType.Capacity,
                        cage.RoomType.DailyPrice,
                        cage.RoomType.HasAc,
                        cage.RoomType.HasCamera,
                        cage.RoomType.HasPremiumFood,
                        cage.RoomType.Status
                    }
                },
                booking = booking == null ? null : new
                {
                    booking.HotelBookingId,
                    booking.Status,
                    booking.CheckInDate,
                    booking.CheckOutDate,
                    booking.StayDays,
                    booking.FinalAmount,
                    pet = new
                    {
                        booking.Pet.PetId,
                        booking.Pet.Name,
                        booking.Pet.Species,
                        booking.Pet.Breed,
                        booking.Pet.Age,
                        booking.Pet.Weight,
                        booking.Pet.Pathology,
                        booking.Pet.ImageUrl
                    },
                    customer = new
                    {
                        booking.Customer.FullName,
                        booking.Customer.Phone,
                        booking.Customer.Email
                    },
                    addons = booking.BookingAddons.Select(addon => new { addon.Name, addon.Price })
                },
                careLogs,
                maintenanceHistory,
                openMaintenanceLog = maintenanceHistory.FirstOrDefault(log => log.IsOpen),
                availableDestinations
            });
        }

    }
}
