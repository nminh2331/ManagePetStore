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
        [HttpGet("Hotel")]
        public IActionResult Hotel()
        {
            return RedirectToAction(nameof(CageMap));
        }

        [HttpGet("Reception")]
        public Task<IActionResult> Reception(int roomTypePage = 1, int cagePage = 1)
        {
            return HotelWorkspace("checkin", roomTypePage, cagePage);
        }

        // Retain the former URL so bookmarks and existing links continue to work.
        [HttpGet("PetCheckIn")]
        public Task<IActionResult> PetCheckIn(int roomTypePage = 1, int cagePage = 1)
        {
            return Reception(roomTypePage, cagePage);
        }

        [HttpGet("CageMap")]
        public Task<IActionResult> CageMap(int roomTypePage = 1, int cagePage = 1)
        {
            return HotelWorkspace("map", roomTypePage, cagePage);
        }

        [HttpGet("CageCategories")]
        public Task<IActionResult> CageCategories(int roomTypePage = 1, int cagePage = 1)
        {
            return HotelWorkspace("categories", roomTypePage, cagePage);
        }

        private async Task<IActionResult> HotelWorkspace(string pageMode, int roomTypePage = 1, int cagePage = 1)
        {
            ViewBag.HotelPageMode = pageMode;

            // Danh sách RoomTypes có phân trang
            int rtPageSize = 6;
            int totalRoomTypes = await _context.RoomTypes
                .CountAsync(roomType => HotelRoomTypeCatalog.Codes.Contains(roomType.Code));
            int totalRtPages = (int)Math.Ceiling((double)totalRoomTypes / rtPageSize);
            int currentRtPage = roomTypePage < 1 ? 1 : (roomTypePage > totalRtPages ? totalRtPages : roomTypePage);
            if (currentRtPage < 1) currentRtPage = 1;

            var roomTypes = await _context.RoomTypes.AsNoTracking()
                .Where(roomType => HotelRoomTypeCatalog.Codes.Contains(roomType.Code))
                .OrderBy(r => r.RoomTypeId)
                .Skip((currentRtPage - 1) * rtPageSize)
                .Take(rtPageSize)
                .ToListAsync();

            ViewBag.RoomTypes = roomTypes;
            ViewBag.RoomTypePage = currentRtPage;
            ViewBag.TotalRoomTypePages = totalRtPages;
            ViewBag.TotalRoomTypes = totalRoomTypes;

            // Tất cả RoomTypes đang active cho dropdown
            var activeRoomTypes = await _context.RoomTypes.AsNoTracking()
                .Where(r => r.Status && HotelRoomTypeCatalog.Codes.Contains(r.Code))
                .OrderBy(r => r.Type)
                .ToListAsync();
            ViewBag.ActiveRoomTypes = activeRoomTypes;

            ViewBag.HotelFoodOptions = await _context.Products
                .AsNoTracking()
                .Where(product => !product.IsDeleted &&
                                  product.Unit == HotelFoodCatalog.DailyUnit &&
                                  product.Category != null &&
                                  !product.Category.IsDeleted &&
                                  product.Category.Code == HotelFoodCatalog.CategoryCode)
                .OrderBy(product => product.Price)
                .ThenBy(product => product.Name)
                .ToListAsync();

            // Danh sách Cages có phân trang
            int cagePageSize = 8;
            int totalCages = await _context.Cages
                .CountAsync(cage => HotelRoomTypeCatalog.Codes.Contains(cage.RoomType.Code));
            int totalCagePages = (int)Math.Ceiling((double)totalCages / cagePageSize);
            int currentCagePage = cagePage < 1 ? 1 : (cagePage > totalCagePages ? totalCagePages : cagePage);
            if (currentCagePage < 1) currentCagePage = 1;

            var cages = await _context.Cages.AsNoTracking()
                .Include(c => c.RoomType)
                .Where(cage => HotelRoomTypeCatalog.Codes.Contains(cage.RoomType.Code))
                .OrderBy(c => c.CageId)
                .Skip((currentCagePage - 1) * cagePageSize)
                .Take(cagePageSize)
                .ToListAsync();

            ViewBag.Cages = cages;
            ViewBag.CagePage = currentCagePage;
            ViewBag.TotalCagePages = totalCagePages;
            ViewBag.TotalCages = totalCages;

            ViewBag.CageMapCages = await _context.Cages
                .AsNoTracking()
                .Include(c => c.RoomType)
                .Where(cage => HotelRoomTypeCatalog.Codes.Contains(cage.RoomType.Code))
                .OrderBy(c => c.CageId)
                .ToListAsync();

            // Thống kê tổng quan
            ViewBag.TotalCageCount = totalCages;
            ViewBag.EmptyCageCount = await _context.Cages.CountAsync(c => c.Status == "Trống" && HotelRoomTypeCatalog.Codes.Contains(c.RoomType.Code));
            ViewBag.CleaningCageCount = await _context.Cages.CountAsync(c => c.Status == "Đang dọn dẹp" && HotelRoomTypeCatalog.Codes.Contains(c.RoomType.Code));
            ViewBag.LockedCageCount = await _context.Cages.CountAsync(c => c.Status == "Khóa" && HotelRoomTypeCatalog.Codes.Contains(c.RoomType.Code));
            ViewBag.MaintenanceCageCount = await _context.Cages.CountAsync(c =>
                HotelRoomTypeCatalog.Codes.Contains(c.RoomType.Code) &&
                (c.Status == "Bảo trì" || c.Status == "Đang dọn dẹp" || c.Status == "Khóa"));

            // Danh sách HotelBookings đang active
            var activeBookings = await _context.HotelBookings.AsNoTracking()
                .Include(b => b.Pet)
                .Include(b => b.Customer)
                .Include(b => b.Cage)
                    .ThenInclude(c => c.RoomType)
                .Include(b => b.CheckoutStatement)
                    .ThenInclude(statement => statement!.Order)
                .Where(b => ActiveHotelStatuses.Contains(b.Status) &&
                            HotelRoomTypeCatalog.Codes.Contains(b.Cage.RoomType.Code))
                .OrderBy(b => b.CheckInDate)
                .ToListAsync();
            ViewBag.ActiveBookings = activeBookings;
            ViewBag.OccupiedCageCount = activeBookings.Select(b => b.CageId).Distinct().Count();

            var onlineBookings = await _context.HotelBookings.AsNoTracking()
                .Include(b => b.Pet)
                .Include(b => b.Customer)
                .Include(b => b.Cage)
                    .ThenInclude(c => c.RoomType)
                .Include(b => b.FoodPlan)
                .Where(b => b.Status == "Đã đặt" &&
                            HotelRoomTypeCatalog.Codes.Contains(b.Cage.RoomType.Code) &&
                            (!b.CheckOutDate.HasValue || b.CheckOutDate.Value >= DateTime.Today))
                .OrderBy(b => b.CheckInDate)
                .ToListAsync();
            ViewBag.OnlineBookings = onlineBookings;

            ViewBag.PendingCageChangeRequests = await _context.HotelCageChangeRequests
                .AsNoTracking()
                .Include(request => request.HotelBooking).ThenInclude(booking => booking.Pet)
                .Include(request => request.HotelBooking).ThenInclude(booking => booking.Customer)
                .Include(request => request.SourceCage).ThenInclude(cage => cage.RoomType)
                .Include(request => request.TargetCage).ThenInclude(cage => cage.RoomType)
                .Where(request => request.Status == "Pending")
                .OrderBy(request => request.RequestedAt)
                .ToListAsync();

            var onlinePetIds = onlineBookings
                .Select(b => b.PetId)
                .Distinct()
                .ToList();
            var petIdsWithMedicalRecords = await _context.MedicalRecords
                .AsNoTracking()
                .Where(record =>
                    onlinePetIds.Contains(record.PetId) &&
                    record.HotelBookingId == null &&
                    record.Weight > 0)
                .Select(record => record.PetId)
                .Distinct()
                .ToListAsync();
            ViewBag.PetIdsWithMedicalRecords = petIdsWithMedicalRecords.ToHashSet();

            return View("~/Areas/ServiceStaff/Views/SpaServices/Hotel.cshtml");
        }

    }
}
