using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ManagePetStore.Models;

namespace ManagePetStore.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "admin,service")]
    public class RoomTypesController : Controller
    {
        private readonly PetStoreManagementContext _context;

        public RoomTypesController(PetStoreManagementContext context)
        {
            _context = context;
        }

        // =========================================================================
        // 1. DANH SÁCH & BỘ LỌC (INDEX)
        // =========================================================================
        [HttpGet]
        public async Task<IActionResult> Index(string search = "", string statusFilter = "All")
        {
            var query = _context.RoomTypes.AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(r => r.Type.Contains(search) || r.Size.Contains(search));

            if (statusFilter == "Active")
                query = query.Where(r => r.Status == true);
            else if (statusFilter == "Inactive")
                query = query.Where(r => r.Status == false);

            // Thống kê toàn bộ (trước khi lọc)
            var allRoomTypes = await _context.RoomTypes.ToListAsync();
            ViewBag.TotalCount = allRoomTypes.Count;
            ViewBag.ActiveCount = allRoomTypes.Count(r => r.Status);
            ViewBag.InactiveCount = allRoomTypes.Count(r => !r.Status);
            ViewBag.Search = search;
            ViewBag.StatusFilter = statusFilter;
            ViewBag.RoomTypes = await query.OrderBy(r => r.RoomTypeId).ToListAsync();

            return View();
        }

        // =========================================================================
        // 2. TẠO LOẠI PHÒNG MỚI
        // =========================================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(string type, string size, int capacity,
            decimal hourlyPrice, decimal dailyPrice, bool hasAc, bool hasCamera, bool hasPremiumFood)
        {
            if (string.IsNullOrWhiteSpace(type) || string.IsNullOrWhiteSpace(size) || capacity <= 0 || hourlyPrice < 0 || dailyPrice < 0)
            {
                TempData["ErrorMessage"] = "Vui lòng nhập đầy đủ và hợp lệ các thông tin bắt buộc.";
                return RedirectToAction("Index");
            }

            _context.RoomTypes.Add(new RoomType
            {
                Type = type.Trim(),
                Size = size.Trim(),
                Capacity = capacity,
                HourlyPrice = hourlyPrice,
                DailyPrice = dailyPrice,
                HasAc = hasAc,
                HasCamera = hasCamera,
                HasPremiumFood = hasPremiumFood,
                Status = true
            });

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = $"Đã thêm loại phòng \"{type.Trim()}\" thành công.";
            return RedirectToAction("Index");
        }

        // =========================================================================
        // 3. CHỈNH SỬA LOẠI PHÒNG
        // =========================================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int roomTypeId, string type, string size, int capacity,
            decimal hourlyPrice, decimal dailyPrice, bool hasAc, bool hasCamera, bool hasPremiumFood)
        {
            if (string.IsNullOrWhiteSpace(type) || string.IsNullOrWhiteSpace(size) || capacity <= 0)
            {
                TempData["ErrorMessage"] = "Vui lòng nhập đầy đủ thông tin hợp lệ.";
                return RedirectToAction("Index");
            }

            var roomType = await _context.RoomTypes.FindAsync(roomTypeId);
            if (roomType == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy loại phòng cần chỉnh sửa.";
                return RedirectToAction("Index");
            }

            roomType.Type = type.Trim();
            roomType.Size = size.Trim();
            roomType.Capacity = capacity;
            roomType.HourlyPrice = hourlyPrice;
            roomType.DailyPrice = dailyPrice;
            roomType.HasAc = hasAc;
            roomType.HasCamera = hasCamera;
            roomType.HasPremiumFood = hasPremiumFood;

            _context.Entry(roomType).State = EntityState.Modified;
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = $"Đã cập nhật loại phòng \"{type.Trim()}\" thành công.";
            return RedirectToAction("Index");
        }

        // =========================================================================
        // 4. KÍCH HOẠT / TẠM NGỪNG LOẠI PHÒNG
        // =========================================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleStatus(int roomTypeId)
        {
            var roomType = await _context.RoomTypes.FindAsync(roomTypeId);
            if (roomType != null)
            {
                roomType.Status = !roomType.Status;
                _context.Entry(roomType).State = EntityState.Modified;
                await _context.SaveChangesAsync();
                string actionText = roomType.Status ? "kích hoạt" : "tạm ngừng";
                TempData["SuccessMessage"] = $"Đã {actionText} loại phòng \"{roomType.Type}\".";
            }
            return RedirectToAction("Index");
        }
    }
}
