using System;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ManagePetStore.Models;

namespace ManagePetStore.Areas.Service.Controllers
{
    [Area("Service")]
    [Authorize(Roles = "service,admin")]
    public class HomeController : Controller
    {
        private readonly PetStoreManagementContext _context;

        public HomeController(PetStoreManagementContext context)
        {
            _context = context;
        }

        // =========================================================================
        // 1. TỔNG QUAN - DANH SÁCH THÚ CƯNG ĐANG LƯU TRÚ
        // =========================================================================
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var activeBookings = await _context.HotelBookings
                .Include(b => b.Pet)
                .Include(b => b.Customer)
                .Include(b => b.Cage)
                    .ThenInclude(c => c.RoomType)
                .Where(b => b.Status == "Active")
                .OrderBy(b => b.CheckInDate)
                .ToListAsync();

            ViewBag.ActiveBookings = activeBookings;
            ViewBag.TotalActive = activeBookings.Count;

            // Thống kê nhanh
            ViewBag.CheckedInToday = activeBookings.Count(b => b.CheckInDate.Date == DateTime.Today);

            return View();
        }

        // =========================================================================
        // 2. FORM TIẾP NHẬN & KHÁM SỨC KHOẺ BAN ĐẦU (GET)
        // =========================================================================
        [HttpGet]
        public async Task<IActionResult> CheckIn(int id)
        {
            var booking = await _context.HotelBookings
                .Include(b => b.Pet)
                    .ThenInclude(p => p.PetBioTimelines)
                .Include(b => b.Customer)
                .Include(b => b.Cage)
                    .ThenInclude(c => c.RoomType)
                .FirstOrDefaultAsync(b => b.HotelBookingId == id && b.Status == "Active");

            if (booking == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy thông tin lưu trú hoặc thú cưng đã rời đi.";
                return RedirectToAction("Index");
            }

            return View(booking);
        }

        // =========================================================================
        // 3. XỬ LÝ SUBMIT KHÁM SỨC KHOẺ (POST)
        // =========================================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CheckIn(int hotelBookingId, decimal weight,
            string coatCondition, bool hasInjury, string? injuryNote,
            string? behaviorNote, string? generalNote)
        {
            if (weight <= 0 || weight > 200)
            {
                TempData["ErrorMessage"] = "Cân nặng không hợp lệ. Vui lòng nhập lại.";
                return RedirectToAction("CheckIn", new { id = hotelBookingId });
            }

            var booking = await _context.HotelBookings
                .Include(b => b.Pet)
                .FirstOrDefaultAsync(b => b.HotelBookingId == hotelBookingId && b.Status == "Active");

            if (booking == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy thông tin lưu trú.";
                return RedirectToAction("Index");
            }

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // 1. Cập nhật cân nặng thú cưng
                var pet = booking.Pet;
                pet.Weight = weight;
                _context.Entry(pet).State = EntityState.Modified;

                // 2. Tạo bản ghi PetBioTimeline
                var staffName = User.FindFirst("FullName")?.Value ?? "Nhân viên dịch vụ";
                var description = BuildCheckInDescription(weight, coatCondition, hasInjury, injuryNote, behaviorNote, generalNote, staffName);

                _context.PetBioTimelines.Add(new PetBioTimeline
                {
                    PetId = pet.PetId,
                    Date = DateTime.Now,
                    Title = $"Tiếp nhận KS #{hotelBookingId:D4} — Khám sức khoẻ ban đầu",
                    Description = description,
                    Type = "CheckIn"
                });

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                TempData["SuccessMessage"] = $"Đã ghi nhận sức khoẻ ban đầu cho {pet.Name} thành công! Hồ sơ đã được lưu vào lịch sử sinh học.";
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                TempData["ErrorMessage"] = "Có lỗi xảy ra khi lưu dữ liệu: " + ex.Message;
            }

            return RedirectToAction("Index");
        }

        // =========================================================================
        // HELPER: TẠO NỘI DUNG MÔ TẢ KHÁM SỨC KHOẺ
        // =========================================================================
        private static string BuildCheckInDescription(decimal weight, string coatCondition,
            bool hasInjury, string? injuryNote, string? behaviorNote, string? generalNote, string staffName)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"[Cân nặng] {weight:F2} kg");
            sb.AppendLine($"[Tình trạng lông] {coatCondition}");
            sb.AppendLine($"[Vết thương / Tổn thương] {(hasInjury ? "CÓ" : "Không có")}");
            if (hasInjury && !string.IsNullOrWhiteSpace(injuryNote))
                sb.AppendLine($"[Mô tả vết thương] {injuryNote.Trim()}");
            if (!string.IsNullOrWhiteSpace(behaviorNote))
                sb.AppendLine($"[Biểu hiện tâm lý] {behaviorNote.Trim()}");
            if (!string.IsNullOrWhiteSpace(generalNote))
                sb.AppendLine($"[Ghi chú tổng quát] {generalNote.Trim()}");
            sb.AppendLine($"[Nhân viên tiếp nhận] {staffName}");
            return sb.ToString().Trim();
        }
    }
}
