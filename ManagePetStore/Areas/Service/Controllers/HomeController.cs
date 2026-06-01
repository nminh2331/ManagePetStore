using System;
using System.Linq;
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
        // 1. TRANG CHỦ
        // =========================================================================
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var activeBookings = await _context.HotelBookings
                .Include(b => b.Pet).ThenInclude(p => p.PetBioTimelines)
                .Include(b => b.Customer)
                .Include(b => b.Cage).ThenInclude(c => c.RoomType)
                .Where(b => b.Status == "Active")
                .OrderBy(b => b.CheckInDate)
                .ToListAsync();

            var pendingCheckIn = activeBookings
                .Where(b => b.Pet != null && !b.Pet.PetBioTimelines.Any(t => t.Type == "CheckIn"))
                .ToList();

            var assessedBookings = activeBookings
                .Where(b => b.Pet != null && b.Pet.PetBioTimelines.Any(t => t.Type == "CheckIn"))
                .ToList();

            ViewBag.ActiveBookings  = activeBookings;
            ViewBag.PendingCheckIn  = pendingCheckIn;
            ViewBag.AssessedBookings = assessedBookings;
            ViewBag.TotalActive    = activeBookings.Count;
            ViewBag.PendingCount   = pendingCheckIn.Count;
            ViewBag.AssessedCount  = assessedBookings.Count;
            ViewBag.CheckedInToday = activeBookings.Count(b => b.CheckInDate.Date == DateTime.Today);

            // Dữ liệu cho modal tạo booking
            ViewBag.Customers = await _context.Customers.OrderBy(c => c.FullName).ToListAsync();
            ViewBag.Cages = await _context.Cages
                .Include(c => c.RoomType)
                .Where(c => c.Status == "Trống" || c.Status == "Empty" || c.Status == "Available")
                .OrderBy(c => c.CageId)
                .ToListAsync();

            return View();
        }

        // =========================================================================
        // API: Lấy pets theo customer (AJAX)
        // =========================================================================
        [HttpGet]
        public async Task<IActionResult> GetPetsByCustomer(int customerId)
        {
            var pets = await _context.Pets
                .Where(p => p.CustomerId == customerId)
                .Select(p => new { p.PetId, p.Name, p.Species, p.Breed, p.Weight, p.Age, p.Pathology })
                .ToListAsync();
            return Json(pets);
        }

        // =========================================================================
        // API: Lấy chuồng trống (AJAX)
        // =========================================================================
        [HttpGet]
        public async Task<IActionResult> GetAvailableCages()
        {
            var cages = await _context.Cages
                .Include(c => c.RoomType)
                .Where(c => c.Status == "Trống" || c.Status == "Empty" || c.Status == "Available")
                .Select(c => new
                {
                    c.CageId,
                    c.Status,
                    roomType = c.RoomType.Type,
                    dailyPrice = c.RoomType.DailyPrice
                })
                .OrderBy(c => c.CageId)
                .ToListAsync();
            return Json(cages);
        }

        // =========================================================================
        // 2. TẠO YÊU CẦU TIẾP NHẬN MỚI (POST)
        // =========================================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateBooking(
            int customerId, int petId, string cageId,
            DateTime checkInDate, DateTime? checkOutDate)
        {
            if (customerId <= 0 || petId <= 0 || string.IsNullOrWhiteSpace(cageId))
            {
                TempData["ErrorMessage"] = "Vui lòng điền đầy đủ thông tin tiếp nhận.";
                return RedirectToAction("Index");
            }

            // Kiểm tra chuồng có trống không
            var cage = await _context.Cages
                .Include(c => c.RoomType)
                .FirstOrDefaultAsync(c => c.CageId == cageId);

            if (cage == null)
            {
                TempData["ErrorMessage"] = "Chuồng không tồn tại.";
                return RedirectToAction("Index");
            }

            // Kiểm tra trùng lịch
            bool conflict = await _context.HotelBookings.AnyAsync(b =>
                b.CageId == cageId && b.Status == "Active");

            if (conflict)
            {
                TempData["ErrorMessage"] = $"Chuồng {cageId} hiện đã có thú cưng. Vui lòng chọn chuồng khác.";
                return RedirectToAction("Index");
            }

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var stayDays = checkOutDate.HasValue
                    ? Math.Max(1, (int)(checkOutDate.Value - checkInDate).TotalDays)
                    : 1;
                var dailyPrice = cage.RoomType?.DailyPrice ?? 0;
                var subtotal = dailyPrice * stayDays;

                var booking = new HotelBooking
                {
                    CustomerId    = customerId,
                    PetId         = petId,
                    CageId        = cageId,
                    CheckInDate   = checkInDate,
                    CheckOutDate  = checkOutDate,
                    StayDays      = stayDays,
                    BaseDailyPrice = dailyPrice,
                    Subtotal      = subtotal,
                    Discount      = 0,
                    FinalAmount   = subtotal,
                    EarnedPoints  = 0,
                    Status        = "Active"
                };

                _context.HotelBookings.Add(booking);

                // Đánh dấu chuồng là đang sử dụng
                cage.Status = "Đang dùng";
                _context.Entry(cage).State = EntityState.Modified;

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                TempData["SuccessMessage"] = $"Đã tạo yêu cầu tiếp nhận thành công! Booking #{booking.HotelBookingId:D4}.";
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                TempData["ErrorMessage"] = "Có lỗi xảy ra: " + ex.Message;
            }

            return RedirectToAction("Index");
        }

        // =========================================================================
        // 3. SỬA BOOKING (GET — lấy dữ liệu qua JSON cho modal)
        // =========================================================================
        [HttpGet]
        public async Task<IActionResult> GetBooking(int id)
        {
            var b = await _context.HotelBookings
                .Include(b => b.Pet)
                .Include(b => b.Customer)
                .Include(b => b.Cage).ThenInclude(c => c.RoomType)
                .FirstOrDefaultAsync(b => b.HotelBookingId == id && b.Status == "Active");

            if (b == null) return NotFound();

            return Json(new
            {
                b.HotelBookingId,
                b.CustomerId,
                customerName = b.Customer?.FullName,
                customerPhone = b.Customer?.Phone,
                b.PetId,
                petName = b.Pet?.Name,
                petSpecies = b.Pet?.Species,
                petBreed = b.Pet?.Breed ?? "",
                b.CageId,
                roomType = b.Cage?.RoomType?.Type ?? "",
                checkInDate = b.CheckInDate.ToString("yyyy-MM-ddTHH:mm"),
                checkOutDate = b.CheckOutDate.HasValue ? b.CheckOutDate.Value.ToString("yyyy-MM-ddTHH:mm") : "",
                b.StayDays,
                b.BaseDailyPrice,
                b.FinalAmount
            });
        }

        // =========================================================================
        // 4. SỬA BOOKING (POST)
        // =========================================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditBooking(
            int hotelBookingId, string cageId,
            DateTime checkInDate, DateTime? checkOutDate)
        {
            var booking = await _context.HotelBookings
                .Include(b => b.Cage).ThenInclude(c => c.RoomType)
                .Include(b => b.Pet)
                .FirstOrDefaultAsync(b => b.HotelBookingId == hotelBookingId && b.Status == "Active");

            if (booking == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy booking.";
                return RedirectToAction("Index");
            }

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Nếu đổi chuồng
                if (booking.CageId != cageId)
                {
                    // Trả chuồng cũ về trống
                    var oldCage = await _context.Cages.FindAsync(booking.CageId);
                    if (oldCage != null) { oldCage.Status = "Trống"; _context.Entry(oldCage).State = EntityState.Modified; }

                    // Kiểm tra chuồng mới
                    bool conflict = await _context.HotelBookings.AnyAsync(b =>
                        b.CageId == cageId && b.Status == "Active" && b.HotelBookingId != hotelBookingId);
                    if (conflict)
                    {
                        TempData["ErrorMessage"] = $"Chuồng {cageId} đang có thú cưng khác.";
                        await transaction.RollbackAsync();
                        return RedirectToAction("Index");
                    }

                    var newCage = await _context.Cages.Include(c => c.RoomType).FirstOrDefaultAsync(c => c.CageId == cageId);
                    if (newCage != null)
                    {
                        newCage.Status = "Đang dùng";
                        _context.Entry(newCage).State = EntityState.Modified;
                        booking.BaseDailyPrice = newCage.RoomType?.DailyPrice ?? booking.BaseDailyPrice;
                    }

                    booking.CageId = cageId;
                }

                var stayDays = checkOutDate.HasValue
                    ? Math.Max(1, (int)(checkOutDate.Value - checkInDate).TotalDays)
                    : booking.StayDays;

                booking.CheckInDate  = checkInDate;
                booking.CheckOutDate = checkOutDate;
                booking.StayDays     = stayDays;
                booking.Subtotal     = booking.BaseDailyPrice * stayDays;
                booking.FinalAmount  = booking.Subtotal - booking.Discount;
                _context.Entry(booking).State = EntityState.Modified;

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                TempData["SuccessMessage"] = $"Đã cập nhật booking #{hotelBookingId:D4} thành công.";
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                TempData["ErrorMessage"] = "Có lỗi xảy ra: " + ex.Message;
            }

            return RedirectToAction("Index");
        }

        // =========================================================================
        // 5. XÓA BOOKING (POST)
        // =========================================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteBooking(int hotelBookingId)
        {
            var booking = await _context.HotelBookings
                .Include(b => b.Pet).ThenInclude(p => p.PetBioTimelines)
                .Include(b => b.Cage)
                .FirstOrDefaultAsync(b => b.HotelBookingId == hotelBookingId && b.Status == "Active");

            if (booking == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy booking.";
                return RedirectToAction("Index");
            }

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Xóa các PetBioTimeline liên quan đến booking này (CheckIn)
                var timelines = booking.Pet?.PetBioTimelines
                    .Where(t => t.Type == "CheckIn" && t.Title.Contains($"#{hotelBookingId:D4}"))
                    .ToList();
                if (timelines != null && timelines.Any())
                    _context.PetBioTimelines.RemoveRange(timelines);

                // Trả chuồng về trống
                if (booking.Cage != null)
                {
                    booking.Cage.Status = "Trống";
                    _context.Entry(booking.Cage).State = EntityState.Modified;
                }

                _context.HotelBookings.Remove(booking);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                TempData["SuccessMessage"] = $"Đã xóa yêu cầu tiếp nhận #{hotelBookingId:D4}.";
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                TempData["ErrorMessage"] = "Có lỗi xảy ra: " + ex.Message;
            }

            return RedirectToAction("Index");
        }

        // =========================================================================
        // 6. TIẾP NHẬN (CHECKIN) — GHI NHẬN TÌNH TRẠNG (POST)
        // =========================================================================
        [HttpGet]
        public async Task<IActionResult> CheckIn(int id)
        {
            var booking = await _context.HotelBookings
                .Include(b => b.Pet).ThenInclude(p => p.PetBioTimelines)
                .Include(b => b.Customer)
                .Include(b => b.Cage).ThenInclude(c => c.RoomType)
                .FirstOrDefaultAsync(b => b.HotelBookingId == id && b.Status == "Active");

            if (booking == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy thông tin lưu trú.";
                return RedirectToAction("Index");
            }

            return View(booking);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CheckIn(int hotelBookingId, decimal weight,
            string? breed, string? age,
            string coatCondition, bool hasInjury, string? injuryNote,
            string? behaviorNote, string? generalNote,
            string? ownerNotes, string? feedingInstructions, string? medications)
        {
            if (weight <= 0 || weight > 200)
            {
                TempData["ErrorMessage"] = "Cân nặng không hợp lệ.";
                return RedirectToAction("Index");
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
                var pet = booking.Pet;
                pet.Weight = weight;
                if (!string.IsNullOrWhiteSpace(breed)) pet.Breed = breed.Trim();
                if (!string.IsNullOrWhiteSpace(age))   pet.Age   = age.Trim();
                _context.Entry(pet).State = EntityState.Modified;

                var staffName = User.FindFirst("FullName")?.Value ?? "Nhân viên dịch vụ";
                var description = BuildCheckInDescription(
                    weight, coatCondition, hasInjury, injuryNote,
                    behaviorNote, generalNote,
                    ownerNotes, feedingInstructions, medications,
                    staffName);

                _context.PetBioTimelines.Add(new PetBioTimeline
                {
                    PetId       = pet.PetId,
                    Date        = DateTime.Now,
                    Title       = $"Tiếp nhận lưu trú #{hotelBookingId:D4}",
                    Description = description,
                    Type        = "CheckIn"
                });

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                TempData["SuccessMessage"] = $"Đã hoàn tất tiếp nhận {pet.Name}! Thông tin tình trạng đã được lưu.";
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                TempData["ErrorMessage"] = "Có lỗi xảy ra: " + ex.Message;
            }

            return RedirectToAction("Index");
        }

        // =========================================================================
        // 7. SỬA TÌNH TRẠNG TIẾP NHẬN (EditCheckIn)
        // =========================================================================
        [HttpGet]
        public async Task<IActionResult> EditCheckIn(int timelineId)
        {
            var timeline = await _context.PetBioTimelines
                .Include(t => t.Pet)
                .FirstOrDefaultAsync(t => t.TimelineId == timelineId && t.Type == "CheckIn");

            if (timeline == null) return NotFound();

            return Json(new
            {
                timelineId  = timeline.TimelineId,
                petId       = timeline.Pet.PetId,
                petName     = timeline.Pet.Name,
                petSpecies  = timeline.Pet.Species,
                petBreed    = timeline.Pet.Breed ?? "",
                petAge      = timeline.Pet.Age ?? "",
                petWeight   = timeline.Pet.Weight,
                petPathology = timeline.Pet.Pathology ?? "",
                description = timeline.Description,
                date        = timeline.Date.ToString("dd/MM/yyyy HH:mm")
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditCheckIn(int timelineId, decimal weight,
            string? breed, string? age,
            string coatCondition, bool hasInjury, string? injuryNote,
            string? behaviorNote, string? generalNote,
            string? ownerNotes, string? feedingInstructions, string? medications)
        {
            var timeline = await _context.PetBioTimelines
                .Include(t => t.Pet)
                .FirstOrDefaultAsync(t => t.TimelineId == timelineId && t.Type == "CheckIn");

            if (timeline == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy bản ghi tiếp nhận.";
                return RedirectToAction("Index");
            }

            if (weight <= 0 || weight > 200)
            {
                TempData["ErrorMessage"] = "Cân nặng không hợp lệ.";
                return RedirectToAction("Index");
            }

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var pet = timeline.Pet;
                pet.Weight = weight;
                if (!string.IsNullOrWhiteSpace(breed)) pet.Breed = breed.Trim();
                if (!string.IsNullOrWhiteSpace(age))   pet.Age   = age.Trim();
                _context.Entry(pet).State = EntityState.Modified;

                var staffName = User.FindFirst("FullName")?.Value ?? "Nhân viên dịch vụ";
                timeline.Description = BuildCheckInDescription(
                    weight, coatCondition, hasInjury, injuryNote,
                    behaviorNote, generalNote,
                    ownerNotes, feedingInstructions, medications,
                    staffName);
                timeline.Date = DateTime.Now;
                _context.Entry(timeline).State = EntityState.Modified;

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                TempData["SuccessMessage"] = $"Đã cập nhật thông tin tiếp nhận của {pet.Name}.";
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                TempData["ErrorMessage"] = "Có lỗi xảy ra: " + ex.Message;
            }

            return RedirectToAction("Index");
        }

        // =========================================================================
        // 8. XÓA TÌNH TRẠNG TIẾP NHẬN (DeleteCheckIn)
        // =========================================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteCheckIn(int timelineId)
        {
            var timeline = await _context.PetBioTimelines
                .Include(t => t.Pet)
                .FirstOrDefaultAsync(t => t.TimelineId == timelineId && t.Type == "CheckIn");

            if (timeline == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy bản ghi tiếp nhận.";
                return RedirectToAction("Index");
            }

            try
            {
                var petName = timeline.Pet?.Name ?? "thú cưng";
                _context.PetBioTimelines.Remove(timeline);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = $"Đã xóa bản ghi tình trạng của {petName}. Thú cưng trở về trạng thái chờ tiếp nhận.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Có lỗi xảy ra khi xóa: " + ex.Message;
            }

            return RedirectToAction("Index");
        }

        // =========================================================================
        // HELPER
        // =========================================================================
        private static string BuildCheckInDescription(decimal weight, string coatCondition,
            bool hasInjury, string? injuryNote, string? behaviorNote, string? generalNote,
            string? ownerNotes, string? feedingInstructions, string? medications,
            string staffName)
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== TIẾP NHẬN ===");
            if (!string.IsNullOrWhiteSpace(ownerNotes))
                sb.AppendLine($"[Dặn dò từ chủ nuôi] {ownerNotes.Trim()}");
            if (!string.IsNullOrWhiteSpace(feedingInstructions))
                sb.AppendLine($"[Hướng dẫn cho ăn] {feedingInstructions.Trim()}");
            if (!string.IsNullOrWhiteSpace(medications))
                sb.AppendLine($"[Thuốc cần uống] {medications.Trim()}");

            sb.AppendLine("=== TÌNH TRẠNG SỨC KHỎE ===");
            sb.AppendLine($"[Cân nặng] {weight:F2} kg");
            sb.AppendLine($"[Tình trạng lông] {coatCondition}");
            sb.AppendLine($"[Vết thương] {(hasInjury ? "CÓ" : "Không có")}");
            if (hasInjury && !string.IsNullOrWhiteSpace(injuryNote))
                sb.AppendLine($"[Mô tả vết thương] {injuryNote.Trim()}");
            if (!string.IsNullOrWhiteSpace(behaviorNote))
                sb.AppendLine($"[Tâm lý / Hành vi] {behaviorNote.Trim()}");
            if (!string.IsNullOrWhiteSpace(generalNote))
                sb.AppendLine($"[Ghi chú khác] {generalNote.Trim()}");
            sb.AppendLine($"[Nhân viên tiếp nhận] {staffName}");
            return sb.ToString().Trim();
        }
    }
}
