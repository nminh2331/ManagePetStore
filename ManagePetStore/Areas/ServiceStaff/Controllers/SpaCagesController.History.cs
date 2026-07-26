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
        // [nam] Tìm kiếm, lọc và phân trang lịch sử booking Hotel dành cho Staff.
        [HttpGet("HotelHistory")]
        public async Task<IActionResult> HotelHistory(
            string? searchTerm,
            string statusFilter = "all",
            int? petId = null,
            int page = 1)
        {
            const int pageSize = 10;
            string normalizedSearch = searchTerm?.Trim() ?? string.Empty;
            string normalizedStatus = string.IsNullOrWhiteSpace(statusFilter)
                ? "all"
                : statusFilter.Trim().ToLowerInvariant();

            var query = _context.HotelBookings
                .AsNoTracking()
                .Include(booking => booking.Pet)
                .Include(booking => booking.Customer)
                .Include(booking => booking.Cage)
                    .ThenInclude(cage => cage.RoomType)
                .AsQueryable();

            if (petId.HasValue)
            {
                query = query.Where(booking => booking.PetId == petId.Value);
            }

            if (!string.IsNullOrWhiteSpace(normalizedSearch))
            {
                int? bookingIdSearch = null;
                string numericSearch = normalizedSearch.StartsWith("HB", StringComparison.OrdinalIgnoreCase)
                    ? normalizedSearch[2..]
                    : normalizedSearch;
                if (int.TryParse(numericSearch, out int parsedBookingId))
                {
                    bookingIdSearch = parsedBookingId;
                }

                query = query.Where(booking =>
                    booking.Pet.Name.Contains(normalizedSearch) ||
                    booking.Pet.Species.Contains(normalizedSearch) ||
                    booking.Customer.FullName.Contains(normalizedSearch) ||
                    booking.Customer.Phone.Contains(normalizedSearch) ||
                    booking.CageId.Contains(normalizedSearch) ||
                    (bookingIdSearch.HasValue && booking.HotelBookingId == bookingIdSearch.Value));
            }

            query = normalizedStatus switch
            {
                "reserved" => query.Where(booking => booking.Status == "Đã đặt"),
                "active" => query.Where(booking => booking.Status == "Active" || booking.Status == "Đang ở"),
                "completed" => query.Where(booking => booking.Status == "Đã trả"),
                "cancelled" => query.Where(booking => booking.Status == "Đã hủy" || booking.Status == "Cancelled" || booking.Status == "Từ chối tiếp nhận"),
                _ => query
            };

            int totalItems = await query.CountAsync();
            int totalPages = totalItems == 0 ? 0 : (int)Math.Ceiling(totalItems / (double)pageSize);
            int currentPage = Math.Max(1, page);
            if (totalPages > 0 && currentPage > totalPages)
            {
                currentPage = totalPages;
            }

            var bookings = await query
                .OrderByDescending(booking => booking.HotelBookingId)
                .Skip((currentPage - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var pets = await _context.Pets
                .AsNoTracking()
                .Where(pet => pet.HotelBookings.Any())
                .OrderBy(pet => pet.Name)
                .ThenBy(pet => pet.PetId)
                .Select(pet => new StaffHotelPetOptionViewModel
                {
                    PetId = pet.PetId,
                    PetName = pet.Name,
                    Species = pet.Species,
                    CustomerName = pet.Customer.FullName,
                    BookingCount = pet.HotelBookings.Count
                })
                .ToListAsync();

            var model = new StaffHotelBookingHistoryPageViewModel
            {
                SearchTerm = normalizedSearch,
                StatusFilter = normalizedStatus,
                PetId = petId,
                Page = currentPage,
                PageSize = pageSize,
                TotalItems = totalItems,
                TotalPages = totalPages,
                Pets = pets,
                Bookings = bookings.Select(booking => new StaffHotelBookingHistoryRowViewModel
                {
                    HotelBookingId = booking.HotelBookingId,
                    PetId = booking.PetId,
                    PetName = booking.Pet.Name,
                    PetSpecies = booking.Pet.Species,
                    CustomerName = booking.Customer.FullName,
                    CustomerPhone = booking.Customer.Phone,
                    CageId = booking.CageId,
                    RoomTypeName = booking.Cage.RoomType.Type,
                    CheckInDate = booking.ScheduledCheckInDate ?? booking.CheckInDate,
                    CheckOutDate = booking.ScheduledCheckOutDate ?? booking.CheckOutDate,
                    Status = booking.Status,
                    StatusKey = ResolveHotelStatusKey(booking.Status),
                    FinalAmount = booking.FinalAmount
                }).ToList()
            };

            return View("~/Areas/ServiceStaff/Views/SpaServices/HotelHistory.cshtml", model);
        }

        // [nam] Hiển thị toàn bộ hoạt động và thay đổi của một lần lưu trú cho Staff.
        [HttpGet("HotelHistory/{id:int}")]
        public async Task<IActionResult> HotelHistoryDetails(int id)
        {
            var booking = await _historyService.GetDetailAsync(id);
            if (booking == null)
            {
                return NotFound();
            }

            return View(
                "~/Areas/ServiceStaff/Views/SpaServices/HotelHistoryDetails.cshtml",
                new StaffHotelBookingDetailPageViewModel { Booking = booking });
        }

    }
}
