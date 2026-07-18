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
    [Area("ServiceStaff")]
    [Authorize(Roles = "service,admin,manager")]
    [Route("SpaServices")]
    public class SpaMedicalRecordsController : Controller
    {
        private static readonly string[] ActiveHotelStatuses = ["Active", "Đang ở"];
        private static readonly string[] BlockingHotelStatuses = ["Đã đặt", "Active", "Đang ở"];

        private readonly PetStoreManagementContext _context;
        private readonly IHotelBookingHistoryService _historyService;
        private readonly IHotelCareMediaService _hotelCareMediaService;
        private readonly IHubContext<HotelCareHub> _hotelCareHub;
        private readonly IHotelCheckoutService _hotelCheckoutService;
        private readonly IInventoryBatchService _inventoryBatchService;
        private readonly IHotelEmailService _hotelEmailService;
        private readonly ILogger<SpaMedicalRecordsController> _logger;

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
                    healthStatus = item.HealthStatus ?? "Chưa ghi nhận",
                    symptoms = item.Symptoms ?? "Không ghi nhận",
                    treatment = item.Treatment ?? "Không ghi nhận",
                    vaccinationStatus = item.VaccinationStatus ?? "Chưa ghi nhận",
                    parasitePrevention = item.ParasitePrevention ?? "Chưa ghi nhận",
                    physicalCheck = item.PhysicalCheck ?? "Không ghi nhận",
                    shellStatus = item.ShellStatus ?? "Không ghi nhận",
                    rearingConditions = item.RearingConditions ?? "Không ghi nhận",
                    abnormalSymptoms = item.AbnormalSymptoms ?? "Không ghi nhận",
                    incisorCheck = item.IncisorCheck ?? "Không ghi nhận",
                    furSkinCheck = item.FurSkinCheck ?? "Không ghi nhận",
                    digestiveSigns = item.DigestiveSigns ?? "Không ghi nhận"
                })
                .FirstOrDefaultAsync();

            return record == null
                ? NotFound(new { success = false, message = "Sổ y tế này không còn khả dụng để tiếp nhận lưu trú chuồng." })
                : Json(new { success = true, record });
        }

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

            int? activeHotelBookingId = await _context.HotelBookings
                .AsNoTracking()
                .Where(booking =>
                    booking.PetId == petId &&
                    ActiveHotelStatuses.Contains(booking.Status))
                .OrderByDescending(booking => booking.CheckInDate)
                .Select(booking => (int?)booking.HotelBookingId)
                .FirstOrDefaultAsync();

            var record = new MedicalRecord
            {
                PetId = petId,
                HotelBookingId = activeHotelBookingId,
                DateCreated = DateTime.Now,
                Weight = weight,
                HealthStatus = healthStatus,
                Symptoms = symptoms,
                Treatment = treatment,
                VaccinationStatus = vaccinationStatus,
                ParasitePrevention = parasitePrevention != null ? string.Join(", ", parasitePrevention) : "",
                PhysicalCheck = physicalCheck,
                ShellStatus = shellStatus,
                RearingConditions = rearingConditions,
                AbnormalSymptoms = abnormalSymptoms != null ? string.Join(", ", abnormalSymptoms) : "",
                IncisorCheck = incisorCheck,
                FurSkinCheck = furSkinCheck,
                DigestiveSigns = digestiveSigns != null ? string.Join(", ", digestiveSigns) : ""
            };

            pet.Weight = weight;
            
            _context.MedicalRecords.Add(record);
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
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Tạo sổ y tế thành công!" });
        }
    }
}
