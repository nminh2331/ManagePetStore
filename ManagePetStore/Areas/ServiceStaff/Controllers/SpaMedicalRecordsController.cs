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
    /// <summary>
    /// Controller quản lý sổ y tế cho dịch vụ Spa & Lưu trú thú cưng.
    /// Cho phép nhân viên dịch vụ xem danh sách thú cưng theo loài, tìm kiếm theo tên thú cưng/chủ nuôi,
    /// xem lịch sử khám bệnh và tạo mới sổ y tế với thông tin đặc thù theo loài (Chó, Mèo, Rùa, Chuột).
    /// </summary>
    [Area("ServiceStaff")]
    [Authorize(Roles = "service,admin,manager")]
    [Route("SpaServices")]
    public class SpaMedicalRecordsController : Controller
    {
        /// <summary> Trạng thái đặt phòng khách sạn đang hoạt động </summary>
        private static readonly string[] ActiveHotelStatuses = ["Active", "Đang ở"];
        
        /// <summary> Trạng thái đặt phòng ngăn cản tạo tiếp nhận mới </summary>
        private static readonly string[] BlockingHotelStatuses = ["Đã đặt", "Active", "Đang ở"];

        private readonly PetStoreManagementContext _context;
        private readonly IHotelBookingHistoryService _historyService;
        private readonly IHotelCareMediaService _hotelCareMediaService;
        private readonly IHubContext<HotelCareHub> _hotelCareHub;
        private readonly IHotelCheckoutService _hotelCheckoutService;
        private readonly IInventoryBatchService _inventoryBatchService;
        private readonly IHotelEmailService _hotelEmailService;
        private readonly ILogger<SpaMedicalRecordsController> _logger;

        /// <summary>
        /// Khởi tạo SpaMedicalRecordsController với các dependency dịch vụ cần thiết.
        /// </summary>
        public SpaMedicalRecordsController(
            PetStoreManagementContext context,
            IHotelBookingHistoryService historyService,
            IHotelCareMediaService hotelCareMediaService,
            IHubContext<HotelCareHub> hotelCareHub,
            IHotelCheckoutService hotelCheckoutService,
            IInventoryBatchService inventoryBatchService,
            IHotelEmailService hotelEmailService,
            ILogger<SpaMedicalRecordsController> logger)
        {
            _context = context;
            _historyService = historyService;
            _hotelCareMediaService = hotelCareMediaService;
            _hotelCareHub = hotelCareHub;
            _hotelCheckoutService = hotelCheckoutService;
            _inventoryBatchService = inventoryBatchService;
            _hotelEmailService = hotelEmailService;
            _logger = logger;
        }

        /// <summary>
        /// Lấy thông tin snapshot của nhân viên đang đăng nhập (ID và Họ tên).
        /// </summary>
        /// <returns>Tuple chứa UserId (nếu có) và Họ tên nhân viên.</returns>
        private (int? UserId, string Name) GetCurrentStaffSnapshot()
        {
            int? userId = null;
            string? userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (int.TryParse(userIdValue, out int parsedUserId))
            {
                userId = parsedUserId;
            }

            string staffName = User.FindFirst("FullName")?.Value
                ?? User.Identity?.Name
                ?? "Nhân viên dịch vụ";

            return (userId, staffName);
        }

        /// <summary>
        /// Tìm kiếm các sổ y tế khả dụng để phục vụ việc tiếp nhận lưu trú chuồng (Hotel Check-in).
        /// Hỗ trợ tìm theo số điện thoại khách hàng hoặc ID đơn đặt lịch lưu trú.
        /// </summary>
        /// <param name="phone">Số điện thoại của khách hàng cần tìm kiếm.</param>
        /// <param name="hotelBookingId">ID đơn đặt lịch lưu trú (nếu tìm theo đơn đặt online).</param>
        /// <returns>JSON kết quả chứa danh sách sổ y tế hợp lệ của khách hàng.</returns>
        [HttpGet("SearchAvailableHotelMedicalRecords")]
        public async Task<IActionResult> SearchAvailableHotelMedicalRecords(string? phone, int? hotelBookingId = null)
        {
            ManagePetStore.Models.Customer? customer;
            int? reservedPetId = null;
            string? reservedPetName = null;
            string? reservedPetSpecies = null;
            if (hotelBookingId.HasValue)
            {
                var reservation = await _context.HotelBookings
                    .AsNoTracking()
                    .Include(booking => booking.Customer)
                    .Include(booking => booking.Pet)
                    .FirstOrDefaultAsync(booking =>
                        booking.HotelBookingId == hotelBookingId.Value &&
                        booking.Status == "Đã đặt");
                if (reservation == null)
                {
                    return NotFound(new { success = false, message = "Lịch đặt online không còn khả dụng." });
                }

                customer = reservation.Customer;
                reservedPetId = reservation.PetId;
                reservedPetName = reservation.Pet.Name;
                reservedPetSpecies = reservation.Pet.Species;
            }
            else
            {
                string normalizedPhone = new((phone ?? string.Empty).Where(char.IsDigit).ToArray());
                if (normalizedPhone.Length is < 10 or > 11)
                {
                    return BadRequest(new { success = false, message = "Số điện thoại không hợp lệ." });
                }

                customer = await _context.Customers
                    .AsNoTracking()
                    .FirstOrDefaultAsync(item => item.Phone == normalizedPhone);
                if (customer == null)
                {
                    return NotFound(new { success = false, message = "Không tìm thấy khách hàng theo số điện thoại này." });
                }
            }

            int excludedBookingId = hotelBookingId ?? 0;
            var records = await _context.MedicalRecords
                .AsNoTracking()
                .Where(record =>
                    record.HotelBookingId == null &&
                    record.Weight > 0 &&
                    record.Pet.CustomerId == customer.CustomerId &&
                    record.Pet.Status == "Active" &&
                    (!reservedPetId.HasValue || record.PetId == reservedPetId.Value) &&
                    !record.Pet.HotelBookings.Any(booking =>
                        booking.HotelBookingId != excludedBookingId &&
                        BlockingHotelStatuses.Contains(booking.Status)))
                .OrderBy(record => record.Pet.Name)
                .ThenByDescending(record => record.DateCreated)
                .Select(record => new
                {
                    recordId = record.RecordId,
                    petId = record.PetId,
                    petName = record.Pet.Name,
                    species = record.Pet.Species,
                    breed = record.Pet.Breed ?? "Chưa rõ",
                    dateCreated = record.DateCreated.ToString("dd/MM/yyyy HH:mm"),
                    healthStatus = record.HealthStatus
                })
                .ToListAsync();

            return Json(new
            {
                success = true,
                customerName = customer.FullName,
                customerPhone = customer.Phone,
                reservedPetId,
                reservedPetName,
                reservedPetSpecies,
                records
            });
        }

        /// <summary>
        /// Lấy thông tin tóm tắt chi tiết của một sổ y tế cụ thể để hiển thị khi tiếp nhận gửi thú cưng.
        /// </summary>
        /// <param name="recordId">Mã sổ y tế cần xem thông tin tóm tắt.</param>
        /// <returns>JSON chứa thông tin tóm tắt chi tiết của sổ y tế.</returns>
        [HttpGet("GetHotelMedicalRecordSummary")]
        public async Task<IActionResult> GetHotelMedicalRecordSummary(int recordId)
        {
            var record = await _context.MedicalRecords
                .AsNoTracking()
                .Where(item => item.RecordId == recordId &&
                               item.HotelBookingId == null &&
                               item.Weight > 0)
                .Select(item => new
                {
                    item.RecordId,
                    petId = item.PetId,
                    petName = item.Pet.Name,
                    species = item.Pet.Species,
                    breed = item.Pet.Breed ?? "Chưa rõ",
                    age = item.Pet.Age ?? "Chưa rõ",
                    customerName = item.Pet.Customer.FullName,
                    customerPhone = item.Pet.Customer.Phone,
                    dateCreated = item.DateCreated.ToString("dd/MM/yyyy HH:mm"),
                    item.Weight,
                    // [nam][Flow] Trả dữ liệu gốc; giao diện tiếp nhận chỉ hiển thị trường có giá trị và đúng loài.
                    healthStatus = item.HealthStatus,
                    symptoms = item.Symptoms,
                    treatment = item.Treatment,
                    vaccinationStatus = item.VaccinationStatus,
                    parasitePrevention = item.ParasitePrevention,
                    physicalCheck = item.PhysicalCheck,
                    shellStatus = item.ShellStatus,
                    rearingConditions = item.RearingConditions,
                    abnormalSymptoms = item.AbnormalSymptoms,
                    incisorCheck = item.IncisorCheck,
                    furSkinCheck = item.FurSkinCheck,
                    digestiveSigns = item.DigestiveSigns
                })
                .FirstOrDefaultAsync();

            return record == null
                ? NotFound(new { success = false, message = "Sổ y tế này không còn khả dụng để tiếp nhận lưu trú chuồng." })
                : Json(new { success = true, record });
        }

        /// <summary>
        /// Hiển thị trang quản lý sổ y tế thú cưng.
        /// Hỗ trợ lọc theo loài (Chó, Mèo, Rùa, Chuột) hoặc chỉ định trước ID thú cưng/đơn lưu trú.
        /// </summary>
        /// <param name="species">Tên loài thú cưng (Chó, Mèo, Rùa, Chuột).</param>
        /// <param name="petId">Mã thú cưng được chọn trước (nếu từ trang tiếp nhận chuyển sang).</param>
        /// <param name="hotelBookingId">Mã đơn lưu trú (nếu có).</param>
        /// <param name="returnUrl">Đường dẫn quay lại sau khi hoàn tất tạo sổ y tế.</param>
        /// <returns>View quản lý sổ y tế kèm dữ liệu danh sách thú cưng.</returns>
        [HttpGet("MedicalRecords")]
        public async Task<IActionResult> MedicalRecords(
            string? species,
            int? petId = null,
            int? hotelBookingId = null,
            string? returnUrl = null)
        {
            if (petId.HasValue)
            {
                if (hotelBookingId.HasValue)
                {
                    bool validReservation = await _context.HotelBookings
                        .AsNoTracking()
                        .AnyAsync(booking =>
                            booking.HotelBookingId == hotelBookingId.Value &&
                            booking.PetId == petId.Value &&
                            booking.Status == "Đã đặt");
                    if (!validReservation)
                    {
                        return NotFound("Booking không còn khả dụng hoặc không khớp với thú cưng.");
                    }
                }

                var selectedPet = await _context.Pets
                    .Include(pet => pet.Customer)
                    .Include(pet => pet.MedicalRecords)
                    .FirstOrDefaultAsync(pet => pet.PetId == petId.Value && pet.Status == "Active");
                if (selectedPet == null)
                {
                    return NotFound("Không tìm thấy hồ sơ thú cưng đang hoạt động.");
                }

                ViewBag.SelectedSpecies = selectedPet.Species;
                ViewBag.Pets = new List<Pet> { selectedPet };
                ViewBag.PreselectedPetId = selectedPet.PetId;
                ViewBag.ReturnUrl = Url.IsLocalUrl(returnUrl) ? returnUrl : "/SpaServices/PetCheckIn";
                return View("~/Areas/ServiceStaff/Views/SpaServices/MedicalRecords.cshtml");
            }

            ViewBag.SelectedSpecies = species;
            ViewBag.ReturnUrl = Url.IsLocalUrl(returnUrl) ? returnUrl : null;
            
            if (!string.IsNullOrEmpty(species))
            {
                var pets = await _context.Pets
                    .Include(p => p.Customer)
                    .Include(p => p.MedicalRecords)
                    .Where(p => p.Species.ToLower() == species.ToLower() || 
                               (species.ToLower() == "chuột" && p.Species.ToLower() == "hamster") ||
                               (species.ToLower() == "rùa" && p.Species.ToLower() == "turtle"))
                    .ToListAsync();
                
                ViewBag.Pets = pets;
            }
            
            return View("~/Areas/ServiceStaff/Views/SpaServices/MedicalRecords.cshtml");
        }

        /// <summary>
        /// Lấy toàn bộ lịch sử các lần tạo/cập nhật sổ y tế của một thú cưng.
        /// </summary>
        /// <param name="petId">Mã thú cưng cần lấy lịch sử sổ y tế.</param>
        /// <returns>JSON chứa danh sách các bản ghi lịch sử sổ khám y tế.</returns>
        [HttpGet("GetPetMedicalHistory")]
        public async Task<IActionResult> GetPetMedicalHistory(int petId)
        {
            var records = await _context.MedicalRecords
                .Where(r => r.PetId == petId)
                .OrderByDescending(r => r.DateCreated)
                .Select(r => new {
                    r.RecordId,
                    r.PetId,
                    DateCreated = r.DateCreated.ToString("dd/MM/yyyy HH:mm"),
                    r.Weight,
                    r.HealthStatus,
                    Symptoms = r.Symptoms ?? "",
                    Treatment = r.Treatment ?? "",
                    VaccinationStatus = r.VaccinationStatus ?? "",
                    ParasitePrevention = r.ParasitePrevention ?? "",
                    PhysicalCheck = r.PhysicalCheck ?? "",
                    ShellStatus = r.ShellStatus ?? "",
                    RearingConditions = r.RearingConditions ?? "",
                    AbnormalSymptoms = r.AbnormalSymptoms ?? "",
                    IncisorCheck = r.IncisorCheck ?? "",
                    FurSkinCheck = r.FurSkinCheck ?? "",
                    DigestiveSigns = r.DigestiveSigns ?? ""
                })
                .ToListAsync();
            return Json(records);
        }

        /// <summary>
        /// Xử lý tạo mới một bản ghi sổ y tế cho thú cưng.
        /// Lưu trữ thông tin sức khỏe chung, thông tin đặc thù theo loài, cập nhật cân nặng/bệnh lý thú cưng,
        /// ghi mốc thời gian lưu trú (nếu có) và gửi thông báo Realtime/SignalR tới chủ nuôi.
        /// </summary>
        /// <param name="petId">ID thú cưng được khám.</param>
        /// <param name="weight">Cân nặng hiện tại (kg).</param>
        /// <param name="healthStatus">Trạng thái sức khỏe tổng quan (Khỏe mạnh, Cần theo dõi, Đang ốm).</param>
        /// <param name="symptoms">Mô tả chẩn đoán / triệu chứng.</param>
        /// <param name="treatment">Hướng xử lý / Kê đơn thuốc.</param>
        /// <param name="vaccinationStatus">Tình trạng tiêm phòng (Chó/Mèo).</param>
        /// <param name="parasitePrevention">Các biện pháp phòng ngừa ký sinh trùng (Chó/Mèo).</param>
        /// <param name="physicalCheck">Kiểm tra thể chất sơ bộ (Chó/Mèo).</param>
        /// <param name="shellStatus">Tình trạng mai rùa (Rùa).</param>
        /// <param name="rearingConditions">Điều kiện nuôi tại nhà (Rùa).</param>
        /// <param name="abnormalSymptoms">Các biểu hiện bất thường (Rùa).</param>
        /// <param name="incisorCheck">Kiểm tra răng cửa (Chuột/Hamster).</param>
        /// <param name="furSkinCheck">Kiểm tra lông/da (Chuột/Hamster).</param>
        /// <param name="digestiveSigns">Dấu hiệu tiêu hóa (Chuột/Hamster).</param>
        /// <returns>JSON kết quả thông báo thành công hoặc thất bại.</returns>
        [HttpPost("CreateMedicalRecord")]
        public async Task<IActionResult> CreateMedicalRecord(
            int petId, 
            decimal weight, 
            string healthStatus, 
            string? symptoms, 
            string? treatment,
            string? vaccinationStatus,
            string[]? parasitePrevention,
            string? physicalCheck,
            string? shellStatus,
            string? rearingConditions,
            string[]? abnormalSymptoms,
            string? incisorCheck,
            string? furSkinCheck,
            string[]? digestiveSigns)
        {
            var pet = await _context.Pets.FindAsync(petId);
            if (pet == null)
            {
                return Json(new { success = false, message = "Không tìm thấy thú cưng." });
            }

            if (weight <= 0 || weight > 999.99m)
            {
                return Json(new { success = false, message = "Cân nặng trong sổ y tế phải lớn hơn 0 và không vượt quá 999,99kg." });
            }

            if (string.IsNullOrWhiteSpace(healthStatus))
            {
                return Json(new { success = false, message = "Phải ghi nhận tình trạng sức khỏe trong sổ y tế." });
            }

            string normalizedSpecies = pet.Species.Trim().ToLowerInvariant();
            bool isDogOrCat = normalizedSpecies is "chó" or "mèo" or "dog" or "cat";
            bool isTurtle = normalizedSpecies is "rùa" or "turtle";
            bool isRodent = normalizedSpecies is "chuột" or "hamster" or "mouse";

            // Kiểm tra xem thú cưng có đơn đặt lưu trú chuồng đang hoạt động không
            int? activeHotelBookingId = await _context.HotelBookings
                .AsNoTracking()
                .Where(booking =>
                    booking.PetId == petId &&
                    ActiveHotelStatuses.Contains(booking.Status))
                .OrderByDescending(booking => booking.CheckInDate)
                .Select(booking => (int?)booking.HotelBookingId)
                .FirstOrDefaultAsync();

            // [BR] Chỉ lưu nhóm thông tin lâm sàng đúng loài. Đây cũng là lớp bảo vệ
            // khi client cũ hoặc request sửa tay gửi kèm giá trị mặc định của nhóm đang ẩn.
            var record = new MedicalRecord
            {
                PetId = petId,
                HotelBookingId = activeHotelBookingId,
                DateCreated = DateTime.Now,
                Weight = weight,
                HealthStatus = healthStatus.Trim(),
                Symptoms = NormalizeOptionalText(symptoms),
                Treatment = NormalizeOptionalText(treatment),
                VaccinationStatus = isDogOrCat ? NormalizeOptionalText(vaccinationStatus) : null,
                ParasitePrevention = isDogOrCat ? JoinSelections(parasitePrevention) : null,
                PhysicalCheck = isDogOrCat ? NormalizeOptionalText(physicalCheck) : null,
                ShellStatus = isTurtle ? NormalizeOptionalText(shellStatus) : null,
                RearingConditions = isTurtle ? NormalizeOptionalText(rearingConditions) : null,
                AbnormalSymptoms = isTurtle ? JoinSelections(abnormalSymptoms) : null,
                IncisorCheck = isRodent ? NormalizeOptionalText(incisorCheck) : null,
                FurSkinCheck = isRodent ? NormalizeOptionalText(furSkinCheck) : null,
                DigestiveSigns = isRodent ? JoinSelections(digestiveSigns) : null
            };

            // Cập nhật thông tin cân nặng và trạng thái bệnh lý gần nhất cho thú cưng
            pet.Weight = weight;
            if (!string.IsNullOrWhiteSpace(healthStatus))
            {
                pet.Pathology = healthStatus.Trim();
            }
            
            _context.MedicalRecords.Add(record);

            // Nếu đang trong quá trình lưu trú chuồng, tạo mốc thời gian nhật ký chăm sóc
            if (activeHotelBookingId.HasValue)
            {
                var staff = GetCurrentStaffSnapshot();
                _context.PetBioTimelines.Add(new PetBioTimeline
                {
                    PetId = petId,
                    HotelBookingId = activeHotelBookingId,
                    Date = record.DateCreated,
                    Title = "Cập nhật hồ sơ y tế",
                    Type = "HotelMedicalUpdate",
                    Description = $"Tình trạng: {healthStatus}. Cân nặng: {weight:0.##} kg. Nhân viên: {staff.Name}."
                });
            }

            // Gửi thông báo đến tài khoản chủ nuôi
            var notification = new CustomerNotification
            {
                CustomerId = pet.CustomerId,
                HotelBookingId = activeHotelBookingId,
                Type = "MedicalRecord",
                Title = $"Sổ y tế mới cho {pet.Name}",
                Message = $"Bé {pet.Name} vừa được cập nhật sổ y tế mới. Tình trạng: {healthStatus}, Cân nặng: {weight:0.##} kg.",
                LinkUrl = $"/Customer/Pet/MedicalHistory?petId={petId}",
                IsRead = false,
                CreatedAt = record.DateCreated
            };
            _context.CustomerNotifications.Add(notification);
            await _context.SaveChangesAsync();

            // Phát bản tin Realtime qua SignalR cho giao diện khách hàng
            try
            {
                await _hotelCareHub.Clients
                    .Group(HotelCareHub.GroupName(pet.CustomerId))
                    .SendAsync("CareLogUpdated", new
                    {
                        notificationId = notification.NotificationId,
                        bookingId = activeHotelBookingId,
                        petName = pet.Name,
                        title = notification.Title,
                        message = notification.Message,
                        mediaUrl = (string?)null,
                        mediaType = (string?)null,
                        occurredAt = record.DateCreated,
                        linkUrl = notification.LinkUrl
                    });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Cannot send SignalR notification for medical record of pet {PetId}.", petId);
            }

            return Json(new { success = true, message = "Tạo sổ y tế thành công!" });
        }

        // [Validate] Chuẩn hóa trường tùy chọn để database không chứa chuỗi rỗng giả dữ liệu.
        private static string? NormalizeOptionalText(string? value) =>
            string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        // [Validate] Loại lựa chọn rỗng/trùng trước khi lưu các nhóm checkbox của sổ y tế.
        private static string? JoinSelections(string[]? values)
        {
            if (values == null)
            {
                return null;
            }

            string[] normalizedValues = values
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            return normalizedValues.Length == 0 ? null : string.Join(", ", normalizedValues);
        }
    }
}
