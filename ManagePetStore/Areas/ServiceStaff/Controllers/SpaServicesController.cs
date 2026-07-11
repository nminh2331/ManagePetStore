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
using CustomerEntity = ManagePetStore.Models.Customer;

namespace ManagePetStore.Areas.ServiceStaff.Controllers
{
    [Area("ServiceStaff")]
    [Authorize(Roles = "service,admin,manager")]
    [Route("SpaServices")]
    public class SpaServicesController : Controller
    {
        private static readonly string[] ActiveHotelStatuses = ["Active", "Đang ở"];
        private static readonly string[] BlockingHotelStatuses = ["Đã đặt", "Active", "Đang ở"];
        private static readonly string[] EditableCageStatuses = ["Trống", "Đang dọn dẹp", "Bảo trì", "Khóa"];
        private static readonly string[] MaintenanceCageStatuses = ["Đang dọn dẹp", "Bảo trì", "Khóa"];
        private const decimal MinimumRoomTypeDailyPrice = 150000m;
        private const decimal MinimumRoomTypeHourlyPrice = 40000m;

        private readonly PetStoreManagementContext _context;
        private readonly IHotelBookingHistoryService _historyService;
        private readonly IHotelCareMediaService _hotelCareMediaService;
        private readonly IHubContext<HotelCareHub> _hotelCareHub;
        private readonly IHotelCheckoutService _hotelCheckoutService;
        private readonly ILogger<SpaServicesController> _logger;

        public SpaServicesController(
            PetStoreManagementContext context,
            IHotelBookingHistoryService historyService,
            IHotelCareMediaService hotelCareMediaService,
            IHubContext<HotelCareHub> hotelCareHub,
            IHotelCheckoutService hotelCheckoutService,
            ILogger<SpaServicesController> logger)
        {
            _context = context;
            _historyService = historyService;
            _hotelCareMediaService = hotelCareMediaService;
            _hotelCareHub = hotelCareHub;
            _hotelCheckoutService = hotelCheckoutService;
            _logger = logger;
        }

        // =========================================================================
        // 1. MÀN HÌNH CHÍNH (ĐƯỜNG DẪN /SpaServices)
        // =========================================================================
        [HttpGet("")]
        [HttpGet("Index")]
        public async Task<IActionResult> Index(DateTime? date, int servicePage = 1, int groomerPage = 1, int queuePage = 1, int walkInPage = 1)
        {
            var selectedDate = date ?? DateTime.Today;
            ViewBag.SelectedDate = selectedDate;

            // Phân hệ 4.1: Danh mục dịch vụ Spa có PHÂN TRANG (PageSize = 5)
            int pageSize = 5;
            int totalServices = await _context.SpaServices.CountAsync();
            int totalPages = (int)Math.Ceiling((double)totalServices / pageSize);
            int currentPage = servicePage < 1 ? 1 : (servicePage > totalPages ? totalPages : servicePage);
            if (currentPage < 1) currentPage = 1;

            var services = await _context.SpaServices
                .OrderBy(s => s.ServiceId)
                .Skip((currentPage - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
            
            ViewBag.Services = services;
            ViewBag.CurrentPage = currentPage;
            ViewBag.TotalPages = totalPages;

            // Chỉ lấy dịch vụ đang active cho các Dropdown
            var activeServices = await _context.SpaServices
                .Where(s => s.Active)
                .OrderBy(s => s.ServiceId)
                .ToListAsync();
            ViewBag.ActiveServices = activeServices;

            // Phân hệ 4.2: Phân ca Nhân viên có PHÂN TRANG (PageSize = 3)
            int groomerPageSize = 3;
            int activeGroomersCount = await _context.Users
                .Include(u => u.Role)
                .Where(u => u.Role.RoleName == "service" && u.Status == "Active")
                .CountAsync();
            ViewBag.ActiveGroomersCount = activeGroomersCount;

            int totalGroomerPages = (int)Math.Ceiling((double)activeGroomersCount / groomerPageSize);
            int currentGroomerPage = groomerPage < 1 ? 1 : (groomerPage > totalGroomerPages ? totalGroomerPages : groomerPage);
            if (currentGroomerPage < 1) currentGroomerPage = 1;

            var groomers = await _context.Users
                .Include(u => u.Role)
                .Where(u => u.Role.RoleName == "service" && u.Status == "Active")
                .OrderBy(u => u.UserId)
                .Skip((currentGroomerPage - 1) * groomerPageSize)
                .Take(groomerPageSize)
                .ToListAsync();

            var allGroomers = await _context.Users
                .Include(u => u.Role)
                .Where(u => u.Role.RoleName == "service" && u.Status == "Active")
                .OrderBy(u => u.UserId)
                .ToListAsync();

            ViewBag.Groomers = groomers;
            ViewBag.AllGroomers = allGroomers;
            ViewBag.GroomerPage = currentGroomerPage;
            ViewBag.TotalGroomerPages = totalGroomerPages;

            var bookings = await _context.SpaBookings
                .Include(b => b.Pet)
                .Include(b => b.Customer)
                .Include(b => b.Service)
                .Where(b => b.DateTime.Date == selectedDate.Date)
                .ToListAsync();
            ViewBag.Bookings = bookings;

            // Phân hệ 4.3: Hàng đợi Spa Real-time có PHÂN TRANG (PageSize = 4)
            int queuePageSize = 4;
            int totalQueueItems = await _context.SpaQueues.CountAsync(q => !q.QueueNumber.StartsWith("PEND-WI-"));
            int totalQueuePages = (int)Math.Ceiling((double)totalQueueItems / queuePageSize);
            int currentQueuePage = queuePage < 1 ? 1 : (queuePage > totalQueuePages ? totalQueuePages : queuePage);
            if (currentQueuePage < 1) currentQueuePage = 1;

            var queue = await _context.SpaQueues
                .Where(q => !q.QueueNumber.StartsWith("PEND-WI-"))
                .OrderBy(q => q.ArrivalTime)
                .Skip((currentQueuePage - 1) * queuePageSize)
                .Take(queuePageSize)
                .ToListAsync();

            ViewBag.Queue = queue;
            ViewBag.QueuePage = currentQueuePage;
            ViewBag.TotalQueuePages = totalQueuePages;
            ViewBag.TotalQueueItems = totalQueueItems;

            // Khách vãng lai chờ (bắt đầu bằng PEND-WI-) có PHÂN TRANG (PageSize = 1)
            var walkInItems = await _context.SpaQueues
                .Where(q => q.QueueNumber.StartsWith("PEND-WI-"))
                .OrderBy(q => q.ArrivalTime)
                .ToListAsync();

            int walkInPageSize = 1;
            int totalWalkIns = walkInItems.Count;
            int totalWalkInPages = (int)Math.Ceiling((double)totalWalkIns / walkInPageSize);
            int currentWalkInPage = walkInPage < 1 ? 1 : (walkInPage > totalWalkInPages ? totalWalkInPages : walkInPage);
            if (currentWalkInPage < 1) currentWalkInPage = 1;

            var paginatedWalkIns = walkInItems
                .Skip((currentWalkInPage - 1) * walkInPageSize)
                .Take(walkInPageSize)
                .ToList();

            var firstWalkInItem = paginatedWalkIns.FirstOrDefault();
            ViewBag.WalkIn = firstWalkInItem;
            ViewBag.WalkInPage = currentWalkInPage;
            ViewBag.TotalWalkInPages = totalWalkInPages;
            ViewBag.TotalWalkIns = totalWalkIns;

            if (firstWalkInItem != null)
            {
                string phone = "";
                if (firstWalkInItem.OwnerName.Contains("(") && firstWalkInItem.OwnerName.Contains(")"))
                {
                    int startIndex = firstWalkInItem.OwnerName.LastIndexOf("(") + 1;
                    int endIndex = firstWalkInItem.OwnerName.LastIndexOf(")");
                    if (startIndex > 0 && endIndex > startIndex)
                    {
                        phone = firstWalkInItem.OwnerName.Substring(startIndex, endIndex - startIndex).Trim();
                    }
                }

                var customer = await _context.Customers.FirstOrDefaultAsync(c => c.Phone == phone);
                var pet = customer != null ? await _context.Pets.FirstOrDefaultAsync(p => p.CustomerId == customer.CustomerId && p.Name == firstWalkInItem.PetName) : null;
                ViewBag.WalkInPet = pet;
            }

            // Dữ liệu dropdowns
            var customers = await _context.Customers
                .Include(c => c.Pets)
                .OrderBy(c => c.FullName)
                .ToListAsync();
            ViewBag.Customers = customers;

            return View("~/Areas/ServiceStaff/Views/SpaServices/Index.cshtml");
        }

        // =========================================================================
        // 2. PHÂN HỆ 4.1: QUẢN LÝ DANH MỤC DỊCH VỤ SPA (CRUD & TOGGLE ACTIVE)
        // =========================================================================
        
        [HttpPost("AddService")]
        public async Task<IActionResult> AddService(string name, int duration, decimal price, string? targetSpecies)
        {
            if (string.IsNullOrWhiteSpace(name) || duration <= 0 || price < 0)
            {
                TempData["ErrorMessage"] = "Thông tin dịch vụ không hợp lệ.";
                return RedirectToAction(nameof(Index));
            }

            if (await _context.SpaServices.AnyAsync(s => s.Name.ToLower() == name.Trim().ToLower()))
            {
                TempData["ErrorMessage"] = "Tên dịch vụ Spa này đã tồn tại.";
                return RedirectToAction(nameof(Index));
            }

            var service = new SpaService
            {
                Name = name.Trim(),
                DurationMinutes = duration,
                Price = price,
                Active = true,
                TargetSpecies = string.IsNullOrEmpty(targetSpecies) ? "Tất cả" : targetSpecies.Trim()
            };

            _context.SpaServices.Add(service);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Thêm dịch vụ Spa mới thành công!";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost("EditService")]
        public async Task<IActionResult> EditService(int id, string name, int duration, decimal price, string? targetSpecies)
        {
            var service = await _context.SpaServices.FindAsync(id);
            if (service == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy dịch vụ.";
                return RedirectToAction(nameof(Index));
            }

            if (string.IsNullOrWhiteSpace(name) || duration <= 0 || price < 0)
            {
                TempData["ErrorMessage"] = "Thông tin không hợp lệ.";
                return RedirectToAction(nameof(Index));
            }

            if (await _context.SpaServices.AnyAsync(s => s.Name.ToLower() == name.Trim().ToLower() && s.ServiceId != id))
            {
                TempData["ErrorMessage"] = "Tên dịch vụ Spa này đã tồn tại.";
                return RedirectToAction(nameof(Index));
            }

            service.Name = name.Trim();
            service.DurationMinutes = duration;
            service.Price = price;
            service.TargetSpecies = string.IsNullOrEmpty(targetSpecies) ? "Tất cả" : targetSpecies.Trim();

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Cập nhật dịch vụ Spa thành công!";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost("DeleteService")]
        public async Task<IActionResult> DeleteService(int id)
        {
            var service = await _context.SpaServices.FindAsync(id);
            if (service == null)
            {
                return Json(new { success = false, message = "Không tìm thấy dịch vụ." });
            }

            // Kiểm tra an toàn khóa ngoại
            bool hasBookings = await _context.SpaBookings.AnyAsync(b => b.ServiceId == id);
            bool hasOrderItems = await _context.OrderItems.AnyAsync(o => o.SpaServiceId == id);

            if (hasBookings || hasOrderItems)
            {
                // Soft delete
                service.Active = false;
                await _context.SaveChangesAsync();
                return Json(new { success = true, isSoftDeleted = true, message = "Dịch vụ đã phát sinh dữ liệu (lịch hẹn/hóa đơn). Hệ thống tự động chuyển sang trạng thái Ngưng hoạt động!" });
            }
            else
            {
                // Hard delete
                _context.SpaServices.Remove(service);
                await _context.SaveChangesAsync();
                return Json(new { success = true, isSoftDeleted = false, message = "Xóa dịch vụ Spa thành công!" });
            }
        }

        [HttpPost("ToggleActive")]
        public async Task<IActionResult> ToggleActive(int id)
        {
            var service = await _context.SpaServices.FindAsync(id);
            if (service == null)
            {
                return Json(new { success = false, message = "Không tìm thấy dịch vụ." });
            }

            service.Active = !service.Active;
            await _context.SaveChangesAsync();

            return Json(new { success = true, active = service.Active });
        }

        // =========================================================================
        // 3. TIẾP NHẬN KHÁCH VÃNG LAI & HÀNG ĐỢI REAL-TIME
        // =========================================================================
        
        [HttpPost("QuickCheckIn")]
        public async Task<IActionResult> QuickCheckIn(
            string petName, string species, string breed, string age, decimal weight, 
            string customerName, string phone, int serviceId, string note, string timeSlot)
        {
            string redirectDate = DateTime.Today.ToString("yyyy-MM-dd");

            if (string.IsNullOrWhiteSpace(petName) || string.IsNullOrWhiteSpace(customerName) || string.IsNullOrWhiteSpace(phone) || serviceId <= 0)
            {
                TempData["ErrorMessage"] = "Vui lòng nhập đầy đủ thông tin bắt buộc.";
                return RedirectToAction(nameof(Index));
            }

            var cleanPhone = phone.Trim();
            if (cleanPhone.Length != 10 || !cleanPhone.All(char.IsDigit))
            {
                TempData["ErrorMessage"] = "Số điện thoại không hợp lệ. Số điện thoại phải gồm đúng 10 chữ số và không chứa ký tự chữ.";
                return RedirectToAction(nameof(Index));
            }

            if (weight <= 0 || weight > 200m)
            {
                TempData["ErrorMessage"] = "Cân nặng thú cưng phải lớn hơn 0 và không vượt quá 200 kg.";
                return RedirectToAction(nameof(Index));
            }

            var service = await _context.SpaServices.FindAsync(serviceId);
            if (service == null || !service.Active)
            {
                TempData["ErrorMessage"] = "Dịch vụ đã chọn không hoạt động.";
                return RedirectToAction(nameof(Index));
            }

            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    var customer = await _context.Customers.FirstOrDefaultAsync(c => c.Phone == cleanPhone);
                    if (customer == null)
                    {
                        var cleanCustomerName = customerName.Trim();
                        if (cleanCustomerName.Length > 100)
                        {
                            cleanCustomerName = cleanCustomerName.Substring(0, 100);
                        }

                        customer = new ManagePetStore.Models.Customer
                        {
                            FullName = cleanCustomerName,
                            Phone = cleanPhone,
                            CreatedAt = DateTime.Now,
                            MembershipTier = "Bronze"
                        };
                        _context.Customers.Add(customer);
                        await _context.SaveChangesAsync();
                    }

                    var cleanPetName = petName.Trim();
                    if (cleanPetName.Length > 50)
                    {
                        cleanPetName = cleanPetName.Substring(0, 50);
                    }

                    var cleanSpecies = species?.Trim() ?? "Chó";
                    if (cleanSpecies.Length > 30)
                    {
                        cleanSpecies = cleanSpecies.Substring(0, 30);
                    }

                    var cleanBreed = breed?.Trim() ?? "Không rõ";
                    if (cleanBreed.Length > 50)
                    {
                        cleanBreed = cleanBreed.Substring(0, 50);
                    }

                    var cleanAge = age?.Trim() ?? "Chưa rõ";
                    if (cleanAge.Length > 30)
                    {
                        cleanAge = cleanAge.Substring(0, 30);
                    }

                    var existingPet = await _context.Pets
                        .FirstOrDefaultAsync(p => p.CustomerId == customer.CustomerId 
                                               && p.Name.ToLower() == cleanPetName.ToLower() 
                                               && p.Status == "Active");

                    Pet pet;
                    if (existingPet != null)
                    {
                        pet = existingPet;
                        if (weight > 0)
                        {
                            pet.Weight = weight;
                            _context.Pets.Update(pet);
                        }
                    }
                    else
                    {
                        pet = new Pet
                        {
                            CustomerId = customer.CustomerId,
                            Name = cleanPetName,
                            Species = cleanSpecies,
                            Breed = cleanBreed,
                            Age = cleanAge,
                            Weight = weight > 0 ? weight : 4.5m,
                            Status = "Active"
                        };
                        _context.Pets.Add(pet);
                    }
                    await _context.SaveChangesAsync();

                    int countToday = await _context.SpaQueues.CountAsync(q => q.QueueNumber.StartsWith("WI-") || q.QueueNumber.StartsWith("PEND-WI-"));
                    string queueNumber = $"PEND-WI-{(700 + countToday + 1)}";

                    DateTime arrivalTime = DateTime.Now;
                    if (!string.IsNullOrEmpty(timeSlot) && TimeSpan.TryParse(timeSlot, out TimeSpan ts))
                    {
                        arrivalTime = DateTime.Today.Add(ts);
                    }
                    redirectDate = arrivalTime.ToString("yyyy-MM-dd");

                    var ownerLabel = $"{customer.FullName} ({customer.Phone})";
                    if (ownerLabel.Length > 100)
                    {
                        ownerLabel = ownerLabel.Substring(0, 100);
                    }

                    var queueItem = new SpaQueue
                    {
                        QueueNumber = queueNumber,
                        PetName = pet.Name,
                        OwnerName = ownerLabel,
                        ArrivalTime = arrivalTime,
                        ServiceDescription = service.Name,
                        Note = note?.Trim()
                    };

                    _context.SpaQueues.Add(queueItem);
                    await _context.SaveChangesAsync();

                    await transaction.CommitAsync();

                    TempData["SuccessMessage"] = $"Tiếp nhận khách vãng lai {customer.FullName} & Pet {pet.Name} thành công! Mã số chờ: {queueNumber}";
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    TempData["ErrorMessage"] = $"Lỗi hệ thống: {ex.Message}";
                }
            }

            return RedirectToAction(nameof(Index), new { date = redirectDate });
        }

        [HttpPost("StartQueue")]
        public async Task<IActionResult> StartQueue(int queueId, int groomerId, string? date)
        {
            var queueItem = await _context.SpaQueues.FindAsync(queueId);
            if (queueItem == null)
            {
                return Json(new { success = false, message = "Không tìm thấy hàng đợi." });
            }

            var groomer = await _context.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.UserId == groomerId);
            if (groomer == null || groomer.Status != "Active" || groomer.Role?.RoleName != "service")
            {
                return Json(new { success = false, message = "Kỹ thuật viên không hợp lệ hoặc ngừng hoạt động." });
            }

            var ownerName = queueItem.OwnerName;
            CustomerEntity? customer = null;
            if (ownerName.Contains("(") && ownerName.Contains(")"))
            {
                int startIndex = ownerName.LastIndexOf("(") + 1;
                int endIndex = ownerName.LastIndexOf(")");
                if (startIndex > 0 && endIndex > startIndex)
                {
                    string phone = ownerName.Substring(startIndex, endIndex - startIndex).Trim();
                    customer = await _context.Customers.FirstOrDefaultAsync(c => c.Phone == phone);
                }
            }
            if (customer == null)
            {
                customer = await _context.Customers.FirstOrDefaultAsync(c => c.FullName == ownerName || ownerName.StartsWith(c.FullName));
            }

            if (customer == null)
            {
                return Json(new { success = false, message = "Không khớp được khách hàng." });
            }

            var pet = await _context.Pets.FirstOrDefaultAsync(p => p.CustomerId == customer.CustomerId && p.Name == queueItem.PetName);
            if (pet == null)
            {
                return Json(new { success = false, message = "Không khớp được thú cưng." });
            }

            var service = await _context.SpaServices.FirstOrDefaultAsync(s => s.Name == queueItem.ServiceDescription && s.Active)
                          ?? await _context.SpaServices.FirstOrDefaultAsync(s => s.Active);

            if (service == null)
            {
                return Json(new { success = false, message = "Không có dịch vụ Spa khả dụng." });
            }

            // Determine target booking datetime based on date parameter and ArrivalTime's time of day
            DateTime targetDate = queueItem.ArrivalTime.Date;
            if (!string.IsNullOrEmpty(date) && DateTime.TryParse(date, out var parsedDate))
            {
                targetDate = parsedDate.Date;
            }
            DateTime targetBookingDateTime = targetDate.Add(queueItem.ArrivalTime.TimeOfDay);

            // Kiểm tra trùng lịch của Groomer tại khung giờ này (áp dụng cho cả online và offline - Interval Overlap Check)
            var bookedSlotsToday = await _context.SpaBookings
                .Include(b => b.Service)
                .Where(b => b.GroomerId == groomerId 
                         && b.DateTime.Date == targetBookingDateTime.Date 
                         && b.SpaStatus != "Cancelled")
                .ToListAsync();

            bool isOverlap = bookedSlotsToday.Any(b => {
                var existingStart = b.DateTime;
                var existingEnd = b.DateTime.AddMinutes(b.Service?.DurationMinutes ?? 30);
                var newStart = targetBookingDateTime;
                var newEnd = targetBookingDateTime.AddMinutes(service.DurationMinutes);
                return newStart < existingEnd && existingStart < newEnd;
            });

            if (isOverlap)
            {
                return Json(new { success = false, message = $"Kỹ thuật viên {groomer.FullName} đã có ca làm việc chồng lấp vào lúc {targetBookingDateTime:HH:mm}. Vui lòng chọn Kỹ thuật viên khác!" });
            }

            // Check if there is already a booking created online for this slot/pet/service
            var existingBooking = await _context.SpaBookings
                .FirstOrDefaultAsync(b => b.CustomerId == customer.CustomerId && b.PetId == pet.PetId && b.ServiceId == service.ServiceId && b.DateTime == targetBookingDateTime && b.SpaStatus != "Cancelled");

            if (existingBooking != null)
            {
                // If it already exists, associate it with the groomer starting it and activate progress
                existingBooking.GroomerId = groomerId;
                existingBooking.SpaStatus = "|0"; // Ensure it starts progress
                
                _context.SpaQueues.Remove(queueItem);
                await _context.SaveChangesAsync();
                return Json(new { success = true, message = $"Bắt đầu thực hiện dịch vụ cho thú cưng {pet.Name}!" });
            }

            var booking = new SpaBooking
            {
                CustomerId = customer.CustomerId,
                PetId = pet.PetId,
                ServiceId = service.ServiceId,
                GroomerId = groomer.UserId,
                DateTime = targetBookingDateTime, // Dùng đúng khung giờ kết hợp với ngày được chọn
                Price = service.Price,
                Status = "Chưa thanh toán",
                SpaStatus = "|0", // Khởi tạo index 0 (Tiếp nhận) làm active status ban đầu
                Notes = queueItem.Note
            };

            _context.SpaBookings.Add(booking);
            _context.SpaQueues.Remove(queueItem);
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = $"Bắt đầu thực hiện dịch vụ cho thú cưng {pet.Name}!" });
        }

        // =========================================================================
        // 4. TIẾN ĐỘ & CẬP NHẬT TRẠNG THÁI SPA (TIẾN ĐỘ SPA MODAL)
        // =========================================================================
        
        [HttpGet("GetBookingDetails")]
        public async Task<IActionResult> GetBookingDetails(int bookingId)
        {
            var booking = await _context.SpaBookings
                .Include(b => b.Pet)
                .Include(b => b.Customer)
                .Include(b => b.Service)
                .Include(b => b.Groomer)
                .FirstOrDefaultAsync(b => b.BookingId == bookingId);

            if (booking == null)
            {
                return NotFound();
            }

            // Định nghĩa 5 bước tiến độ chuẩn
            var statuses = new[] { "Tiếp nhận", "Tắm & Sấy", "Cắt & Tỉa", "Massage", "Hoàn thành" };
            var completedSteps = new List<string>();
            var activeStep = "Tiếp nhận";

            // Phân giải SpaStatus (nén index dạng: "0,1|3" -> các bước hoàn thành là [0, 1], bước đang thực hiện là 3)
            var dbStatus = booking.SpaStatus ?? "0";
            if (dbStatus.Contains("|"))
            {
                var parts = dbStatus.Split('|');
                var completedPart = parts[0];
                var activePart = parts[1];

                if (!string.IsNullOrEmpty(completedPart))
                {
                    var indexes = completedPart.Split(',').Select(int.Parse).ToList();
                    foreach (var idx in indexes)
                    {
                        if (idx >= 0 && idx < statuses.Length)
                        {
                            completedSteps.Add(statuses[idx]);
                        }
                    }
                }

                if (int.TryParse(activePart, out int actIdx) && actIdx >= 0 && actIdx < statuses.Length)
                {
                    activeStep = statuses[actIdx];
                }
            }
            else
            {
                // Hỗ trợ fallback tương thích ngược dữ liệu cũ
                if (statuses.Contains(dbStatus))
                {
                    activeStep = dbStatus;
                }
                else if (int.TryParse(dbStatus, out int actIdx) && actIdx >= 0 && actIdx < statuses.Length)
                {
                    activeStep = statuses[actIdx];
                }
            }

            return Json(new
            {
                bookingId = booking.BookingId,
                petName = booking.Pet.Name,
                petSpecies = booking.Pet.Species,
                petBreed = booking.Pet.Breed ?? "Chưa rõ",
                petAge = booking.Pet.Age ?? "Chưa rõ",
                petWeight = booking.Pet.Weight,
                petPathology = booking.Pet.Pathology ?? "Khỏe mạnh, bình thường",
                customerName = booking.Customer.FullName,
                customerPhone = booking.Customer.Phone,
                customerTier = booking.Customer.MembershipTier,
                serviceName = booking.Service.Name,
                groomerName = booking.Groomer.FullName,
                timeSlot = booking.DateTime.ToString("HH:mm"),
                activeStep = activeStep,
                completedSteps = completedSteps,
                notes = booking.Notes ?? "Không có dặn dò đặc biệt."
            });
        }

        [HttpPost("UpdateSpaStatus")]
        public async Task<IActionResult> UpdateSpaStatus(int bookingId, string status)
        {
            var booking = await _context.SpaBookings.FindAsync(bookingId);
            if (booking == null)
            {
                return Json(new { success = false, message = "Không tìm thấy lịch hẹn." });
            }

            var statuses = new[] { "Tiếp nhận", "Tắm & Sấy", "Cắt & Tỉa", "Massage", "Hoàn thành" };
            int newIndex = Array.IndexOf(statuses, status);
            if (newIndex == -1)
            {
                return Json(new { success = false, message = "Trạng thái không hợp lệ." });
            }

            var dbStatus = booking.SpaStatus ?? "0";
            var completedIndexes = new List<int>();
            int activeIndex = 0;

            // Phân giải SpaStatus hiện tại
            if (dbStatus.Contains("|"))
            {
                var parts = dbStatus.Split('|');
                if (!string.IsNullOrEmpty(parts[0]))
                {
                    completedIndexes = parts[0].Split(',').Select(int.Parse).ToList();
                }
                int.TryParse(parts[1], out activeIndex);
            }
            else
            {
                int idx = Array.IndexOf(statuses, dbStatus);
                if (idx != -1) activeIndex = idx;
                else int.TryParse(dbStatus, out activeIndex);
            }

            // Logic tích dấu tuyến tính: tự động hoàn thành tất cả các bước lên đến newIndex (bao gồm cả newIndex)
            completedIndexes.Clear();
            for (int i = 0; i <= newIndex; i++)
            {
                completedIndexes.Add(i);
            }

            activeIndex = newIndex;

            // Đóng gói lưu lại DB dưới dạng nén
            booking.SpaStatus = string.Join(",", completedIndexes) + "|" + activeIndex;

            // Nếu nhân viên hoàn thành trạng thái chăm sóc thú cưng thì lưu vào bảng StaffTasks và đồng bộ hóa đơn POS
            if (status == "Hoàn thành")
            {
                await _context.Entry(booking).Reference(b => b.Pet).LoadAsync();
                await _context.Entry(booking).Reference(b => b.Customer).LoadAsync();
                await _context.Entry(booking).Reference(b => b.Service).LoadAsync();

                // Đồng bộ hóa hóa đơn sang POS (Tạo Order và OrderItem nếu booking chưa thanh toán và chưa có hóa đơn tương ứng)
                if (booking.Status == "Chưa thanh toán")
                {
                    bool hasExistingOrder = await _context.OrderItems
                        .AnyAsync(oi => oi.SpaServiceId == booking.ServiceId 
                                     && oi.Order.CustomerId == booking.CustomerId 
                                     && oi.Order.Status == "Chờ thanh toán" 
                                     && oi.Order.Subtotal == booking.Price);
                    
                    if (!hasExistingOrder)
                    {
                        var orderId = $"ORD-SPA-{bookingId}-{Random.Shared.Next(100, 999)}";
                        var order = new Order
                        {
                            OrderId = orderId,
                            CustomerId = booking.CustomerId,
                            Subtotal = booking.Price,
                            Discount = 0,
                            Total = booking.Price,
                            PaymentMethod = "Tiền mặt",
                            PointsRedeemed = 0,
                            PointsEarned = (int)Math.Floor(booking.Price / 10000m),
                            Status = "Chờ thanh toán",
                            Date = DateTime.Now
                        };
                        _context.Orders.Add(order);

                        var orderItem = new OrderItem
                        {
                            OrderId = orderId,
                            SpaServiceId = booking.ServiceId,
                            Quantity = 1,
                            Price = booking.Price,
                            IsCombo = false
                        };
                        _context.OrderItems.Add(orderItem);
                    }
                }

                string taskId = $"TSK-SPA-{bookingId}";
                var existingTask = await _context.StaffTasks.FirstOrDefaultAsync(t => t.TaskId == taskId);
                if (existingTask == null)
                {
                    var staffTask = new StaffTask
                    {
                        TaskId = taskId,
                        PetId = booking.PetId,
                        CustomerId = booking.CustomerId,
                        AssignedStaffId = booking.GroomerId,
                        ServiceDescription = $"Chăm sóc Spa: {booking.Service?.Name ?? "Dịch vụ Spa"}",
                        ScheduledTime = booking.DateTime.ToString("dd/MM/yyyy HH:mm"),
                        Location = "Khu vực Spa",
                        Priority = "Medium",
                        Status = "Completed",
                        Notes = $"Đã hoàn thành dịch vụ chăm sóc. Ghi chú của khách: {booking.Notes ?? "Không có"}"
                    };
                    _context.StaffTasks.Add(staffTask);
                }
                else
                {
                    existingTask.Status = "Completed";
                }
            }

            await _context.SaveChangesAsync();

            var completedNames = completedIndexes.Select(i => statuses[i]).ToList();
            var activeName = statuses[activeIndex];

            return Json(new { 
                success = true, 
                activeStep = activeName, 
                completedSteps = completedNames,
                message = $"Cập nhật trạng thái thành '{status}'!" 
            });
        }



        [HttpPost("CancelBooking")]
        public async Task<IActionResult> CancelBooking(int bookingId)
        {
            var booking = await _context.SpaBookings.FindAsync(bookingId);
            if (booking == null)
            {
                return Json(new { success = false, message = "Không tìm thấy lịch hẹn." });
            }

            if (booking.SpaStatus != null && booking.SpaStatus != "|0" && booking.SpaStatus != "0" && booking.SpaStatus != "Cancelled")
            {
                return Json(new { success = false, message = "Không thể hủy/xóa lịch hẹn đã bắt đầu thực hiện dịch vụ." });
            }

            // Find and remove associated queue item if this was an online booking
            if (booking.CustomerId > 0 && booking.PetId > 0)
            {
                var customer = await _context.Customers.FindAsync(booking.CustomerId);
                var pet = await _context.Pets.FindAsync(booking.PetId);
                if (customer != null && pet != null)
                {
                    var ownerLabel = $"{customer.FullName} ({customer.Phone})";
                    var queueItem = await _context.SpaQueues
                        .FirstOrDefaultAsync(q => q.PetName == pet.Name && q.OwnerName == ownerLabel && q.ArrivalTime == booking.DateTime);
                    if (queueItem != null)
                    {
                        _context.SpaQueues.Remove(queueItem);
                    }
                }
            }

            _context.SpaBookings.Remove(booking);
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Hủy lịch hẹn thành công!" });
        }


        [HttpGet("GetRealtimeQueue")]
        public async Task<IActionResult> GetRealtimeQueue(int page = 1)
        {
            int pageSize = 4;
            int total = await _context.SpaQueues.CountAsync(q => !q.QueueNumber.StartsWith("PEND-WI-"));
            int totalPages = (int)Math.Ceiling((double)total / pageSize);
            int currentPage = page < 1 ? 1 : (page > totalPages ? totalPages : page);
            if (currentPage < 1) currentPage = 1;

            var queue = await _context.SpaQueues
                .Where(q => !q.QueueNumber.StartsWith("PEND-WI-"))
                .OrderBy(q => q.ArrivalTime)
                .Skip((currentPage - 1) * pageSize)
                .Take(pageSize)
                .Select(q => new {
                    queueId = q.QueueId,
                    queueNumber = q.QueueNumber,
                    petName = q.PetName,
                    ownerName = q.OwnerName,
                    arrivalTime = q.ArrivalTime.ToString("HH:mm"),
                    serviceDescription = q.ServiceDescription,
                    note = q.Note
                })
                .ToListAsync();

            return Json(new { 
                queue = queue, 
                currentPage = currentPage, 
                totalPages = totalPages, 
                totalItems = total 
            });
        }

        [HttpGet("GetWalkInDetails")]
        public async Task<IActionResult> GetWalkInDetails(int queueId)
        {
            var queueItem = await _context.SpaQueues.FindAsync(queueId);
            if (queueItem == null)
            {
                return NotFound();
            }

            string phone = "";
            string customerName = queueItem.OwnerName;
            if (queueItem.OwnerName.Contains("(") && queueItem.OwnerName.Contains(")"))
            {
                int startIndex = queueItem.OwnerName.LastIndexOf("(") + 1;
                int endIndex = queueItem.OwnerName.LastIndexOf(")");
                if (startIndex > 0 && endIndex > startIndex)
                {
                    phone = queueItem.OwnerName.Substring(startIndex, endIndex - startIndex).Trim();
                    customerName = queueItem.OwnerName.Substring(0, queueItem.OwnerName.LastIndexOf("(")).Trim();
                }
            }

            var customer = await _context.Customers.FirstOrDefaultAsync(c => c.Phone == phone);
            var pet = customer != null ? await _context.Pets.FirstOrDefaultAsync(p => p.CustomerId == customer.CustomerId && p.Name == queueItem.PetName) : null;

            var service = await _context.SpaServices.FirstOrDefaultAsync(s => s.Name == queueItem.ServiceDescription);

            return Json(new {
                queueId = queueItem.QueueId,
                queueNumber = queueItem.QueueNumber,
                petName = queueItem.PetName,
                species = pet?.Species ?? "Chó",
                breed = pet?.Breed ?? "Không rõ",
                age = pet?.Age ?? "Chưa rõ",
                weight = pet?.Weight ?? 4.5m,
                pathology = pet?.Pathology ?? "Khỏe mạnh, bình thường",
                customerName = customerName,
                phone = phone,
                serviceId = service?.ServiceId ?? 0,
                serviceName = queueItem.ServiceDescription ?? service?.Name ?? "Dịch vụ Spa",
                timeSlot = queueItem.ArrivalTime.ToString("HH:mm"),
                note = queueItem.Note
            });
        }

        [HttpGet("GetGroomersBusyStatus")]
        public async Task<IActionResult> GetGroomersBusyStatus(string date, string time)
        {
            if (!DateTime.TryParse(date, out DateTime parsedDate))
            {
                parsedDate = DateTime.Today;
            }
            if (!TimeSpan.TryParse(time, out TimeSpan parsedTime))
            {
                parsedTime = new TimeSpan(9, 0, 0);
            }
            DateTime targetDateTime = parsedDate.Date.Add(parsedTime);

            var busyGroomerIds = await _context.SpaBookings
                .Where(b => b.DateTime == targetDateTime && b.SpaStatus != "Cancelled")
                .Select(b => b.GroomerId)
                .ToListAsync();

            return Json(busyGroomerIds);
        }

        [HttpPost("EditWalkIn")]
        public async Task<IActionResult> EditWalkIn(
            int queueId, string petName, string species, string breed, string age, decimal weight, 
            string customerName, string phone, int serviceId, string note, string timeSlot)
        {
            if (queueId <= 0 || string.IsNullOrWhiteSpace(petName) || string.IsNullOrWhiteSpace(customerName) || string.IsNullOrWhiteSpace(phone) || serviceId <= 0)
            {
                TempData["ErrorMessage"] = "Thông tin không hợp lệ.";
                return RedirectToAction(nameof(Index));
            }

            var queueItem = await _context.SpaQueues.FindAsync(queueId);
            if (queueItem == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy thông tin hàng đợi.";
                return RedirectToAction(nameof(Index));
            }

            var service = await _context.SpaServices.FindAsync(serviceId);
            if (service == null || !service.Active)
            {
                TempData["ErrorMessage"] = "Dịch vụ đã chọn không khả dụng.";
                return RedirectToAction(nameof(Index));
            }

            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    // Parse original phone to find original customer
                    string origPhone = "";
                    if (queueItem.OwnerName.Contains("(") && queueItem.OwnerName.Contains(")"))
                    {
                        int startIndex = queueItem.OwnerName.LastIndexOf("(") + 1;
                        int endIndex = queueItem.OwnerName.LastIndexOf(")");
                        if (startIndex > 0 && endIndex > startIndex)
                        {
                            origPhone = queueItem.OwnerName.Substring(startIndex, endIndex - startIndex).Trim();
                        }
                    }

                    var customer = await _context.Customers.FirstOrDefaultAsync(c => c.Phone == origPhone);
                    if (customer != null)
                    {
                        // Cập nhật thông tin Customer
                        customer.FullName = customerName.Trim();
                        customer.Phone = phone.Trim();
                        
                        // Cập nhật thông tin Pet liên quan
                        var pet = await _context.Pets.FirstOrDefaultAsync(p => p.CustomerId == customer.CustomerId && p.Name == queueItem.PetName);
                        if (pet != null)
                        {
                            pet.Name = petName.Trim();
                            pet.Species = species ?? "Chó";
                            pet.Breed = breed?.Trim() ?? "Không rõ";
                            pet.Age = age?.Trim() ?? "Chưa rõ";
                            pet.Weight = weight > 0 ? weight : 4.5m;
                        }
                    }

                    // Cập nhật thông tin hàng đợi
                    DateTime arrivalTime = DateTime.Now;
                    if (!string.IsNullOrEmpty(timeSlot) && TimeSpan.TryParse(timeSlot, out TimeSpan ts))
                    {
                        arrivalTime = DateTime.Today.Add(ts);
                    }

                    queueItem.PetName = petName.Trim();
                    queueItem.OwnerName = $"{customerName.Trim()} ({phone.Trim()})";
                    queueItem.ArrivalTime = arrivalTime;
                    queueItem.ServiceDescription = service.Name;
                    queueItem.Note = note?.Trim();

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    TempData["SuccessMessage"] = "Cập nhật thông tin khách vãng lai thành công!";
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    TempData["ErrorMessage"] = $"Lỗi cập nhật: {ex.Message}";
                }
            }

            return RedirectToAction(nameof(Index), new { date = queueItem.ArrivalTime.ToString("yyyy-MM-dd") });
        }

        [HttpPost("AcceptWalkIn")]
        public async Task<IActionResult> AcceptWalkIn(int queueId)
        {
            var queueItem = await _context.SpaQueues.FindAsync(queueId);
            if (queueItem == null)
            {
                return Json(new { success = false, message = "Không tìm thấy thông tin chờ." });
            }

            if (queueItem.QueueNumber.StartsWith("PEND-WI-"))
            {
                queueItem.QueueNumber = queueItem.QueueNumber.Replace("PEND-WI-", "WI-");
                await _context.SaveChangesAsync();
                return Json(new { success = true, message = "Đã nhận pet vào hàng đợi thành công!" });
            }

            return Json(new { success = false, message = "Pet đã ở trong hàng đợi." });
        }

        [HttpPost("CancelQueueItem")]
        public async Task<IActionResult> CancelQueueItem(int queueId, string reason)
        {
            if (string.IsNullOrWhiteSpace(reason))
            {
                return Json(new { success = false, message = "Vui lòng cung cấp lý do hủy lịch hẹn." });
            }

            var queueItem = await _context.SpaQueues.FindAsync(queueId);
            if (queueItem == null)
            {
                return Json(new { success = false, message = "Không tìm thấy hàng đợi." });
            }

            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    string phone = "";
                    string ownerName = queueItem.OwnerName;
                    if (ownerName.Contains("(") && ownerName.Contains(")"))
                    {
                        int startIndex = ownerName.LastIndexOf("(") + 1;
                        int endIndex = ownerName.LastIndexOf(")");
                        if (startIndex > 0 && endIndex > startIndex)
                        {
                            phone = ownerName.Substring(startIndex, endIndex - startIndex).Trim();
                        }
                    }

                    var customer = await _context.Customers.FirstOrDefaultAsync(c => c.Phone == phone);
                    if (customer != null)
                    {
                        var pet = await _context.Pets.FirstOrDefaultAsync(p => p.CustomerId == customer.CustomerId && p.Name == queueItem.PetName);
                        var service = await _context.SpaServices.FirstOrDefaultAsync(s => s.Name == queueItem.ServiceDescription);
                        
                        if (pet != null && service != null)
                        {
                            DateTime targetBookingDateTime = queueItem.ArrivalTime;
                            var booking = await _context.SpaBookings
                                .FirstOrDefaultAsync(b => b.CustomerId == customer.CustomerId && b.PetId == pet.PetId && b.ServiceId == service.ServiceId && b.DateTime == targetBookingDateTime && b.SpaStatus != "Cancelled");
                            
                            if (booking != null)
                            {
                                booking.SpaStatus = "Cancelled";
                                booking.Notes = $"[Lý do hủy: {reason}] " + (booking.Notes ?? "");
                            }
                            else
                            {
                                // Tạo một lịch hẹn Cancelled cho khách vãng lai để lưu lại lịch sử hủy trong DB
                                var defaultGroomer = await _context.Users.Include(u => u.Role)
                                    .FirstOrDefaultAsync(u => u.Role.RoleName == "service" && u.Status == "Active")
                                    ?? await _context.Users.Include(u => u.Role)
                                    .FirstOrDefaultAsync(u => u.Role.RoleName == "service")
                                    ?? await _context.Users.FirstOrDefaultAsync();
                                int groomerId = defaultGroomer?.UserId ?? 3;

                                var cancelledBooking = new SpaBooking
                                {
                                    CustomerId = customer.CustomerId,
                                    PetId = pet.PetId,
                                    ServiceId = service.ServiceId,
                                    GroomerId = groomerId,
                                    DateTime = targetBookingDateTime,
                                    Price = service.Price,
                                    Status = "Chưa thanh toán",
                                    SpaStatus = "Cancelled",
                                    Notes = $"[Lý do hủy: {reason}] " + (queueItem.Note ?? "")
                                };
                                _context.SpaBookings.Add(cancelledBooking);
                            }
                        }
                    }

                    _context.SpaQueues.Remove(queueItem);
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    return Json(new { success = true, message = "Đã hủy lịch hẹn và xóa khỏi hàng đợi thành công!" });
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    return Json(new { success = false, message = $"Lỗi hệ thống khi hủy: {ex.Message}" });
                }
            }
        }

        // =========================================================================
        // 6. TRANG QUẢN LÝ CHUỒNG & TIẾP NHẬN THÚ CƯNG (HOTEL)
        // =========================================================================

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
                "cancelled" => query.Where(booking => booking.Status == "Đã hủy" || booking.Status == "Cancelled"),
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
                .OrderByDescending(booking => booking.CheckInDate)
                .ThenByDescending(booking => booking.HotelBookingId)
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

        [HttpPost("HotelCareLog")]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(55 * 1024 * 1024)]
        public async Task<IActionResult> CreateHotelCareLog(HotelCareLogRequest request)
        {
            var allowedActivityTypes = new[]
            {
                "General", "Feeding", "Health", "Exercise", "Hygiene", "Medication", "CameraSnapshot"
            };

            if (!allowedActivityTypes.Contains(request.ActivityType, StringComparer.OrdinalIgnoreCase))
            {
                ModelState.AddModelError(nameof(request.ActivityType), "Loại hoạt động không hợp lệ.");
            }

            if (request.IsExtraCharge && request.ExtraChargeAmount <= 0)
            {
                ModelState.AddModelError(nameof(request.ExtraChargeAmount), "Phụ phí bữa ăn phải lớn hơn 0.");
            }

            var booking = await _context.HotelBookings
                .Include(item => item.Pet)
                .Include(item => item.Customer)
                .Include(item => item.FoodPlan)
                .FirstOrDefaultAsync(item => item.HotelBookingId == request.HotelBookingId);

            if (booking == null)
            {
                return NotFound();
            }

            if (!string.Equals(booking.Status, "Active", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(booking.Status, "Đang ở", StringComparison.OrdinalIgnoreCase))
            {
                ModelState.AddModelError(string.Empty, "Chỉ có thể cập nhật nhật ký cho booking đang lưu trú.");
            }

            var occurredAt = request.OccurredAt ?? DateTime.Now;
            var earliestAllowed = (booking.ActualCheckInAt ?? booking.CheckInDate).AddHours(-1);
            if (occurredAt > DateTime.Now.AddMinutes(5) || occurredAt < earliestAllowed)
            {
                ModelState.AddModelError(nameof(request.OccurredAt), "Thời gian nhật ký phải thuộc lượt lưu trú và không được ở tương lai.");
            }

            if (!ModelState.IsValid)
            {
                TempData["HotelCareError"] = ModelState.Values
                    .SelectMany(value => value.Errors)
                    .Select(error => error.ErrorMessage)
                    .FirstOrDefault() ?? "Thông tin nhật ký không hợp lệ.";
                return RedirectToAction(nameof(HotelHistoryDetails), new { id = request.HotelBookingId });
            }

            HotelCareMediaResult? media = null;
            try
            {
                media = await _hotelCareMediaService.SaveAsync(booking.HotelBookingId, request.MediaFile);
            }
            catch (InvalidOperationException ex)
            {
                TempData["HotelCareError"] = ex.Message;
                return RedirectToAction(nameof(HotelHistoryDetails), new { id = request.HotelBookingId });
            }

            var staffUserId = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var parsedUserId)
                ? parsedUserId
                : (int?)null;
            var staffName = User.FindFirstValue("FullName") ?? User.Identity?.Name ?? "Nhân viên";
            var safeTitle = request.Title.Trim();
            var safeStatus = request.Status.Trim();
            var safeNote = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
            var notificationMessage = BuildCareNotificationMessage(safeStatus, safeNote);
            var notificationTitle = $"{booking.Pet.Name}: {safeTitle}";
            if (notificationTitle.Length > 180)
            {
                notificationTitle = notificationTitle[..177] + "...";
            }

            var careLog = new FoodDiaryLog
            {
                LogId = $"FD-{Guid.NewGuid():N}",
                PetName = booking.Pet.Name,
                CageId = booking.CageId,
                HotelBookingId = booking.HotelBookingId,
                ActivityType = request.ActivityType,
                Title = safeTitle,
                Status = safeStatus,
                FoodType = string.IsNullOrWhiteSpace(request.FoodType)
                    ? booking.FoodPlan?.FoodNameSnapshot ?? "Không áp dụng"
                    : request.FoodType.Trim(),
                Amount = request.ServedGrams.HasValue
                    ? $"{request.ServedGrams:0.##} g"
                    : string.IsNullOrWhiteSpace(request.Amount) ? "Không áp dụng" : request.Amount.Trim(),
                PhotoUrl = media?.MediaType == "Image" ? media.PublicUrl : null,
                MediaUrl = media?.PublicUrl,
                MediaType = media?.MediaType,
                Note = safeNote,
                Time = occurredAt.ToString("HH:mm"),
                OccurredAt = occurredAt,
                StaffName = staffName,
                IsVisibleToCustomer = request.IsVisibleToCustomer,
                CreatedByUserId = staffUserId,
                FoodPlanId = request.ActivityType == "Feeding" ? booking.FoodPlan?.FoodPlanId : null,
                MealType = string.IsNullOrWhiteSpace(request.MealType) ? null : request.MealType.Trim(),
                ServedGrams = request.ServedGrams,
                ConsumedPercent = request.ConsumedPercent,
                IsExtraCharge = request.IsExtraCharge,
                ExtraChargeAmount = request.IsExtraCharge ? request.ExtraChargeAmount : 0
            };

            _context.FoodDiaryLogs.Add(careLog);
            _context.PetBioTimelines.Add(new PetBioTimeline
            {
                PetId = booking.PetId,
                HotelBookingId = booking.HotelBookingId,
                Date = occurredAt,
                Title = safeTitle.Length > 100 ? safeTitle[..100] : safeTitle,
                Type = "HotelDailyCare",
                Description = notificationMessage
            });

            CustomerNotification? notification = null;
            if (request.IsVisibleToCustomer)
            {
                notification = new CustomerNotification
                {
                    CustomerId = booking.CustomerId,
                    HotelBookingId = booking.HotelBookingId,
                    Type = "DailyCare",
                    Title = notificationTitle,
                    Message = notificationMessage,
                    LinkUrl = $"/Customer/HotelBooking/Details/{booking.HotelBookingId}",
                    CreatedAt = DateTime.Now
                };
                _context.CustomerNotifications.Add(notification);
            }

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                await _hotelCareMediaService.DeleteAsync(media?.PublicUrl);
                _logger.LogError(ex, "Cannot save daily care log for hotel booking {HotelBookingId}.", booking.HotelBookingId);
                TempData["HotelCareError"] = "Không thể lưu nhật ký lúc này. Vui lòng thử lại.";
                return RedirectToAction(nameof(HotelHistoryDetails), new { id = booking.HotelBookingId });
            }

            if (notification != null)
            {
                try
                {
                    await _hotelCareHub.Clients
                        .Group(HotelCareHub.GroupName(booking.CustomerId))
                        .SendAsync("CareLogUpdated", new
                        {
                            notificationId = notification.NotificationId,
                            bookingId = booking.HotelBookingId,
                            petName = booking.Pet.Name,
                            title = notification.Title,
                            message = notification.Message,
                            mediaUrl = media?.PublicUrl,
                            mediaType = media?.MediaType,
                            occurredAt,
                            linkUrl = notification.LinkUrl
                        });
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Daily care log was saved but realtime delivery failed for customer {CustomerId}.", booking.CustomerId);
                }
            }

            TempData["HotelCareSuccess"] = $"Đã cập nhật nhật ký chăm sóc của {booking.Pet.Name}.";
            return RedirectToAction(nameof(HotelHistoryDetails), new { id = booking.HotelBookingId });
        }

        private static string BuildCareNotificationMessage(string status, string? note)
        {
            var message = string.IsNullOrWhiteSpace(note) ? status : $"{status}. {note}";
            return message.Length > 500 ? message[..497] + "..." : message;
        }

        private static string ResolveHotelStatusKey(string? status)
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
            int totalRoomTypes = await _context.RoomTypes.CountAsync();
            int totalRtPages = (int)Math.Ceiling((double)totalRoomTypes / rtPageSize);
            int currentRtPage = roomTypePage < 1 ? 1 : (roomTypePage > totalRtPages ? totalRtPages : roomTypePage);
            if (currentRtPage < 1) currentRtPage = 1;

            var roomTypes = await _context.RoomTypes
                .OrderBy(r => r.RoomTypeId)
                .Skip((currentRtPage - 1) * rtPageSize)
                .Take(rtPageSize)
                .ToListAsync();

            ViewBag.RoomTypes = roomTypes;
            ViewBag.RoomTypePage = currentRtPage;
            ViewBag.TotalRoomTypePages = totalRtPages;
            ViewBag.TotalRoomTypes = totalRoomTypes;

            // Tất cả RoomTypes đang active cho dropdown
            var activeRoomTypes = await _context.RoomTypes
                .Where(r => r.Status)
                .OrderBy(r => r.Type)
                .ToListAsync();
            ViewBag.ActiveRoomTypes = activeRoomTypes;

            // Danh sách Cages có phân trang
            int cagePageSize = 8;
            int totalCages = await _context.Cages.CountAsync();
            int totalCagePages = (int)Math.Ceiling((double)totalCages / cagePageSize);
            int currentCagePage = cagePage < 1 ? 1 : (cagePage > totalCagePages ? totalCagePages : cagePage);
            if (currentCagePage < 1) currentCagePage = 1;

            var cages = await _context.Cages
                .Include(c => c.RoomType)
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
                .OrderBy(c => c.CageId)
                .ToListAsync();

            // Thống kê tổng quan
            ViewBag.TotalCageCount = await _context.Cages.CountAsync();
            ViewBag.EmptyCageCount = await _context.Cages.CountAsync(c => c.Status == "Trống");
            ViewBag.CleaningCageCount = await _context.Cages.CountAsync(c => c.Status == "Đang dọn dẹp");
            ViewBag.LockedCageCount = await _context.Cages.CountAsync(c => c.Status == "Khóa");
            ViewBag.MaintenanceCageCount = await _context.Cages.CountAsync(c =>
                c.Status == "Bảo trì" || c.Status == "Đang dọn dẹp" || c.Status == "Khóa");

            // Danh sách HotelBookings đang active
            var activeBookings = await _context.HotelBookings
                .Include(b => b.Pet)
                .Include(b => b.Customer)
                .Include(b => b.Cage)
                    .ThenInclude(c => c.RoomType)
                .Where(b => ActiveHotelStatuses.Contains(b.Status))
                .OrderBy(b => b.CheckInDate)
                .ToListAsync();
            ViewBag.ActiveBookings = activeBookings;
            ViewBag.OccupiedCageCount = activeBookings.Select(b => b.CageId).Distinct().Count();

            var onlineBookings = await _context.HotelBookings
                .Include(b => b.Pet)
                .Include(b => b.Customer)
                .Include(b => b.Cage)
                    .ThenInclude(c => c.RoomType)
                .Where(b => b.Status == "Đã đặt" &&
                            (!b.CheckOutDate.HasValue || b.CheckOutDate.Value >= DateTime.Today))
                .OrderBy(b => b.CheckInDate)
                .ToListAsync();
            ViewBag.OnlineBookings = onlineBookings;

            var onlinePetIds = onlineBookings
                .Select(b => b.PetId)
                .Distinct()
                .ToList();
            var petIdsWithMedicalRecords = await _context.MedicalRecords
                .AsNoTracking()
                .Where(record => onlinePetIds.Contains(record.PetId))
                .Select(record => record.PetId)
                .Distinct()
                .ToListAsync();
            ViewBag.PetIdsWithMedicalRecords = petIdsWithMedicalRecords.ToHashSet();

            // Danh sách Customers cho dropdown
            var customers = await _context.Customers
                .Include(c => c.Pets)
                .OrderBy(c => c.FullName)
                .ToListAsync();
            ViewBag.Customers = customers;

            return View("~/Areas/ServiceStaff/Views/SpaServices/Hotel.cshtml");
        }

        // =========================================================================
        // 6.1. TIẾP NHẬN THÚ CƯNG VÀO CHUỒNG (CHECK-IN)
        // =========================================================================

        [HttpGet("GetAvailableCages")]
        public async Task<IActionResult> GetAvailableCages(int roomTypeId)
        {
            var cages = await _context.Cages
                .Where(c => c.RoomTypeId == roomTypeId && c.Status == "Trống")
                .Select(c => new { cageId = c.CageId, status = c.Status })
                .ToListAsync();
            return Json(cages);
        }

        [HttpGet("GetHotelPetsByCustomer")]
        public async Task<IActionResult> GetHotelPetsByCustomer(int customerId)
        {
            var pets = await _context.Pets
                .Where(p => p.CustomerId == customerId && p.Status == "Active")
                .Select(p => new
                {
                    petId = p.PetId,
                    name = p.Name,
                    species = p.Species,
                    breed = p.Breed ?? "Chưa rõ",
                    age = p.Age ?? "Chưa rõ",
                    weight = p.Weight,
                    pathology = p.Pathology ?? "",
                    hasMedicalRecord = _context.MedicalRecords.Any(record => record.PetId == p.PetId)
                })
                .ToListAsync();
            return Json(pets);
        }

        [HttpGet("GetPetMedicalSummary")]
        public async Task<IActionResult> GetPetMedicalSummary(int petId)
        {
            var record = await _context.MedicalRecords
                .AsNoTracking()
                .Where(item => item.PetId == petId)
                .OrderByDescending(item => item.DateCreated)
                .Select(item => new
                {
                    dateCreated = item.DateCreated.ToString("dd/MM/yyyy HH:mm"),
                    item.Weight,
                    healthStatus = item.HealthStatus ?? "Chưa ghi nhận",
                    symptoms = item.Symptoms ?? "Không ghi nhận",
                    vaccinationStatus = item.VaccinationStatus ?? "Chưa ghi nhận",
                    parasitePrevention = item.ParasitePrevention ?? "Chưa ghi nhận",
                    physicalCheck = item.PhysicalCheck ?? "Không có ghi chú"
                })
                .FirstOrDefaultAsync();

            return record == null
                ? NotFound(new { success = false, message = "Thú cưng chưa có sổ y tế trên hệ thống." })
                : Json(new { success = true, record });
        }

        [HttpPost("CheckIn")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CheckIn([FromForm] HotelCheckInRequest request)
        {
            if (!ModelState.IsValid)
            {
                return HotelValidationError(GetModelStateErrorMessage());
            }

            if (request.HealthStatus == HotelCheckInRequest.RejectedStatus)
            {
                return HotelValidationError("Thú cưng được kết luận không đủ điều kiện lưu trú nên không thể nhận vào chuồng.");
            }

            string customerPhone = new(request.CustomerPhone.Where(char.IsDigit).ToArray());
            string cageId = request.CageId.Trim().ToUpperInvariant();
            string petName = request.PetName.Trim();
            string pathology = request.Pathology?.Trim() ?? string.Empty;
            string healthNote = request.HealthNote.Trim();
            DateTime checkInDate = request.CheckInDate!.Value;
            DateTime? checkOutDate = request.CheckOutDate;

            await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            try
            {
                var cage = await _context.Cages
                    .Include(c => c.RoomType)
                    .FirstOrDefaultAsync(c => c.CageId == cageId);

                if (cage == null)
                {
                    return HotelValidationError("Không tìm thấy chuồng đã chọn.");
                }

                if (cage.Status != "Trống")
                {
                    return HotelValidationError($"Chuồng {cageId} hiện không còn trống.");
                }

                if (cage.RoomType == null || !cage.RoomType.Status || cage.RoomTypeId != request.RoomTypeId)
                {
                    return HotelValidationError("Chuồng đã chọn không thuộc loại chuồng đang hoạt động.");
                }

                var customer = await _context.Customers.FirstOrDefaultAsync(c => c.Phone == customerPhone);
                Pet pet;
                bool usingExistingPet = false;

                if (request.ExistingPetId.HasValue)
                {
                    if (customer == null)
                    {
                        return HotelValidationError("Số điện thoại không khớp với chủ của thú cưng đã chọn.");
                    }

                    pet = await _context.Pets.FirstOrDefaultAsync(p => p.PetId == request.ExistingPetId.Value)
                          ?? throw new InvalidOperationException("Không tìm thấy thú cưng đã chọn.");

                    if (pet.CustomerId != customer.CustomerId)
                    {
                        return HotelValidationError("Thú cưng đã chọn không thuộc chủ nuôi có số điện thoại này.");
                    }

                    if (!string.Equals(pet.Status, "Active", StringComparison.OrdinalIgnoreCase))
                    {
                        return HotelValidationError("Hồ sơ thú cưng đã chọn không còn hoạt động.");
                    }

                    pet.Weight = request.Weight;
                    pet.Pathology = string.IsNullOrWhiteSpace(pathology) ? null : pathology;
                    usingExistingPet = true;
                }
                else
                {
                    if (customer == null)
                    {
                        customer = new CustomerEntity
                        {
                            FullName = request.CustomerName.Trim(),
                            Phone = customerPhone,
                            CreatedAt = DateTime.Now,
                            MembershipTier = "Bronze"
                        };
                        _context.Customers.Add(customer);
                        await _context.SaveChangesAsync();
                    }

                    var existingPet = await _context.Pets.FirstOrDefaultAsync(p =>
                        p.CustomerId == customer.CustomerId &&
                        p.Name.ToLower() == petName.ToLower());

                    if (existingPet != null)
                    {
                        if (!string.Equals(existingPet.Status, "Active", StringComparison.OrdinalIgnoreCase))
                        {
                            return HotelValidationError("Hồ sơ thú cưng trùng tên hiện không còn hoạt động.");
                        }

                        pet = existingPet;
                        pet.Species = request.Species.Trim();
                        pet.Breed = string.IsNullOrWhiteSpace(request.Breed) ? pet.Breed : request.Breed.Trim();
                        pet.Age = string.IsNullOrWhiteSpace(request.Age) ? pet.Age : request.Age.Trim();
                        pet.Weight = request.Weight;
                        pet.Pathology = string.IsNullOrWhiteSpace(pathology) ? null : pathology;
                        usingExistingPet = true;
                    }
                    else
                    {
                        pet = new Pet
                        {
                            CustomerId = customer.CustomerId,
                            Name = petName,
                            Species = request.Species.Trim(),
                            Breed = string.IsNullOrWhiteSpace(request.Breed) ? "Không rõ" : request.Breed.Trim(),
                            Age = string.IsNullOrWhiteSpace(request.Age) ? "Chưa rõ" : request.Age.Trim(),
                            Weight = request.Weight,
                            Pathology = string.IsNullOrWhiteSpace(pathology) ? null : pathology,
                            Status = "Active"
                        };
                        _context.Pets.Add(pet);
                        await _context.SaveChangesAsync();
                    }
                }

                bool hasMedicalRecord = await _context.MedicalRecords
                    .AsNoTracking()
                    .AnyAsync(record => record.PetId == pet.PetId);

                if (request.ReceptionMode == HotelCheckInRequest.MedicalRecordMode)
                {
                    if (!usingExistingPet)
                    {
                        return HotelValidationError("Tiếp nhận bằng sổ y tế yêu cầu chọn thú cưng đã có hồ sơ.");
                    }

                    if (!hasMedicalRecord)
                    {
                        return HotelValidationError("Thú cưng được chọn chưa có sổ y tế trên hệ thống. Vui lòng dùng luồng chưa có sổ y tế.");
                    }
                }
                else if (hasMedicalRecord)
                {
                    return HotelValidationError("Thú cưng này đã có sổ y tế. Vui lòng dùng luồng tiếp nhận bằng sổ y tế để bảo toàn lịch sử.");
                }

                bool petIsAlreadyBoarding = await _context.HotelBookings.AnyAsync(b =>
                    b.PetId == pet.PetId && (b.Status == "Active" || b.Status == "Đang ở"));
                if (petIsAlreadyBoarding)
                {
                    return HotelValidationError($"{pet.Name} đang có một lượt lưu trú chưa hoàn tất.");
                }

                HotelBooking? onlineReservation = null;
                if (request.HotelBookingId.HasValue)
                {
                    onlineReservation = await _context.HotelBookings.FirstOrDefaultAsync(b =>
                        b.HotelBookingId == request.HotelBookingId.Value &&
                        b.Status == "Đã đặt");

                    if (onlineReservation == null)
                    {
                        return HotelValidationError("Không tìm thấy lịch đặt online đang chờ tiếp nhận.");
                    }

                    if (onlineReservation.PetId != pet.PetId || onlineReservation.CustomerId != customer.CustomerId)
                    {
                        return HotelValidationError("Lịch đặt online không khớp với chủ nuôi hoặc thú cưng đã chọn.");
                    }

                    if (onlineReservation.CheckInDate.Date > DateTime.Today)
                    {
                        return HotelValidationError("Chưa đến ngày nhận của lịch đặt online này.");
                    }
                }
                else
                {
                    onlineReservation = await _context.HotelBookings.FirstOrDefaultAsync(b =>
                        b.PetId == pet.PetId &&
                        b.CustomerId == customer.CustomerId &&
                        b.Status == "Đã đặt" &&
                        b.CheckInDate.Date == checkInDate.Date);
                }

                if (onlineReservation != null &&
                    !string.Equals(onlineReservation.CageId, cageId, StringComparison.OrdinalIgnoreCase))
                {
                    return HotelValidationError(
                        $"{pet.Name} đã đặt online chuồng {onlineReservation.CageId} trong ngày nhận này. Vui lòng chọn đúng chuồng đã giữ.");
                }

                if (onlineReservation?.CheckOutDate != null)
                {
                    checkOutDate = onlineReservation.CheckOutDate;
                }

                bool petHasScheduleConflict = await _context.HotelBookings.AnyAsync(b =>
                    b.PetId == pet.PetId &&
                    b.HotelBookingId != (onlineReservation != null ? onlineReservation.HotelBookingId : 0) &&
                    (b.Status == "Đã đặt" || b.Status == "Active" || b.Status == "Đang ở") &&
                    (!checkOutDate.HasValue || b.CheckInDate < checkOutDate.Value) &&
                    (!b.CheckOutDate.HasValue || b.CheckOutDate.Value > checkInDate));

                if (petHasScheduleConflict)
                {
                    return HotelValidationError($"{pet.Name} có lịch lưu trú khác trùng với khoảng thời gian tiếp nhận.");
                }

                bool cageHasScheduleConflict = await _context.HotelBookings.AnyAsync(b =>
                    b.CageId == cageId &&
                    b.HotelBookingId != (onlineReservation != null ? onlineReservation.HotelBookingId : 0) &&
                    (b.Status == "Đã đặt" || b.Status == "Active" || b.Status == "Đang ở") &&
                    (!checkOutDate.HasValue || b.CheckInDate < checkOutDate.Value) &&
                    (!b.CheckOutDate.HasValue || b.CheckOutDate.Value > checkInDate));

                if (cageHasScheduleConflict)
                {
                    return HotelValidationError($"Chuồng {cageId} đã được giữ cho một lịch lưu trú khác trong khoảng thời gian này.");
                }

                decimal dailyPrice = cage.RoomType.DailyPrice;
                int stayDays = checkOutDate.HasValue
                    ? Math.Max(1, (int)Math.Ceiling((checkOutDate.Value - checkInDate).TotalDays))
                    : 1;
                decimal subtotal = dailyPrice * stayDays;

                HotelBooking hotelBooking;
                if (onlineReservation != null)
                {
                    onlineReservation.ScheduledCheckInDate ??= onlineReservation.CheckInDate;
                    onlineReservation.ScheduledCheckOutDate ??= onlineReservation.CheckOutDate;
                    onlineReservation.CheckInDate = checkInDate;
                    onlineReservation.ActualCheckInAt = checkInDate;
                    onlineReservation.Status = "Đang ở";
                    hotelBooking = onlineReservation;
                }
                else
                {
                    hotelBooking = new HotelBooking
                    {
                        CageId = cageId,
                        PetId = pet.PetId,
                        CustomerId = customer.CustomerId,
                        CheckInDate = checkInDate,
                        CheckOutDate = checkOutDate,
                        ScheduledCheckInDate = checkInDate,
                        ScheduledCheckOutDate = checkOutDate,
                        ActualCheckInAt = checkInDate,
                        StayDays = stayDays,
                        BaseDailyPrice = dailyPrice,
                        Subtotal = subtotal,
                        Discount = 0,
                        FinalAmount = subtotal,
                        EarnedPoints = 0,
                        Status = "Đang ở"
                    };
                    _context.HotelBookings.Add(hotelBooking);
                }

                _context.PetBioTimelines.Add(new PetBioTimeline
                {
                    PetId = pet.PetId,
                    HotelBooking = hotelBooking,
                    Date = DateTime.Now,
                    Title = "Kiểm tra sức khỏe đầu vào",
                    Type = "HealthCheckIn",
                    Description = BuildHealthCheckDescription(request, pathology, healthNote)
                });

                _context.PetBioTimelines.Add(new PetBioTimeline
                {
                    PetId = pet.PetId,
                    HotelBooking = hotelBooking,
                    Date = DateTime.Now,
                    Title = "Tiếp nhận lưu trú",
                    Type = "PetCheckIn",
                    Description = BuildPetCheckInDescription(request, cageId, customer.FullName)
                });

                if (request.ReceptionMode == HotelCheckInRequest.NoMedicalRecordMode)
                {
                    _context.MedicalRecords.Add(new MedicalRecord
                    {
                        PetId = pet.PetId,
                        HotelBooking = hotelBooking,
                        DateCreated = DateTime.Now,
                        Weight = request.Weight,
                        HealthStatus = request.HealthStatus == HotelCheckInRequest.FitStatus
                            ? "Đủ điều kiện lưu trú"
                            : "Cần theo dõi",
                        Symptoms = string.IsNullOrWhiteSpace(pathology) ? null : pathology,
                        PhysicalCheck = $"Kiểm tra lúc tiếp nhận: {healthNote}\nNhiệt độ cơ thể: {request.BodyTemperature:0.##}°C",
                        VaccinationStatus = "Chưa ghi nhận",
                        ParasitePrevention = "Chưa ghi nhận"
                    });
                }

                cage.Status = "Đang dùng";

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                TempData["HotelSuccess"] = $"Đã hoàn tất tiếp nhận lưu trú cho {pet.Name} tại chuồng {cageId}!";
            }
            catch (InvalidOperationException ex)
            {
                await transaction.RollbackAsync();
                return HotelValidationError(ex.Message);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Không thể kiểm tra sức khỏe và tiếp nhận thú cưng vào chuồng {CageId}", cageId);
                TempData["HotelError"] = "Không thể tiếp nhận thú cưng do lỗi hệ thống. Vui lòng thử lại.";
            }

            return RedirectToAction(nameof(Reception));
        }

        private IActionResult HotelValidationError(string message)
        {
            TempData["HotelError"] = message;
            return RedirectToAction(nameof(Reception));
        }

        private string GetModelStateErrorMessage()
        {
            var errors = ModelState.Values
                .SelectMany(value => value.Errors)
                .Select(error => error.ErrorMessage)
                .Where(message => !string.IsNullOrWhiteSpace(message))
                .Distinct()
                .Take(4)
                .ToList();

            return errors.Count == 0
                ? "Thông tin tiếp nhận không hợp lệ."
                : string.Join(" ", errors);
        }

        private string BuildHealthCheckDescription(HotelCheckInRequest request, string pathology, string healthNote)
        {
            string conclusion = request.HealthStatus == HotelCheckInRequest.FitStatus
                ? "Đủ điều kiện lưu trú"
                : "Đủ điều kiện nhưng cần theo dõi";
            string checkedBy = User.FindFirst("FullName")?.Value ?? User.Identity?.Name ?? "Nhân viên dịch vụ";
            string receptionSource = request.ReceptionMode == HotelCheckInRequest.MedicalRecordMode
                ? "Dùng sổ y tế hiện có"
                : "Chưa có sổ y tế - tạo bản ghi ban đầu";

            return $"Hình thức tiếp nhận: {receptionSource}\n"
                 + $"Kết luận: {conclusion}\n"
                 + $"Cân nặng lúc nhận: {request.Weight:0.##} kg\n"
                 + $"Nhiệt độ cơ thể: {request.BodyTemperature:0.##}°C\n"
                 + $"Bệnh lý/tình trạng đặc biệt: {(string.IsNullOrWhiteSpace(pathology) ? "Không ghi nhận" : pathology)}\n"
                 + $"Ghi chú kiểm tra: {healthNote}\n"
                 + $"Người kiểm tra: {checkedBy}";
        }

        private string BuildPetCheckInDescription(HotelCheckInRequest request, string cageId, string customerName)
        {
            string checkedBy = User.FindFirst("FullName")?.Value ?? User.Identity?.Name ?? "Nhân viên dịch vụ";
            string expectedCheckout = request.CheckOutDate.HasValue
                ? request.CheckOutDate.Value.ToString("dd/MM/yyyy HH:mm")
                : "Chưa xác định";
            string receptionSource = request.ReceptionMode == HotelCheckInRequest.MedicalRecordMode
                ? "Dùng sổ y tế hiện có"
                : "Chưa có sổ y tế";

            return $"Hình thức tiếp nhận: {receptionSource}\n"
                 + $"Chuồng tiếp nhận: {cageId}\n"
                 + $"Chủ thú cưng: {customerName}\n"
                 + $"Ngày nhận: {request.CheckInDate!.Value:dd/MM/yyyy HH:mm}\n"
                 + $"Ngày trả dự kiến: {expectedCheckout}\n"
                 + $"Nhân viên tiếp nhận: {checkedBy}";
        }

        [HttpPost("CheckOut")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CheckOut(int bookingId)
        {
            var booking = await _context.HotelBookings
                .Include(b => b.Cage)
                .Include(b => b.Pet)
                .Include(b => b.CheckoutStatement)
                    .ThenInclude(statement => statement!.Order)
                .FirstOrDefaultAsync(b => b.HotelBookingId == bookingId);

            if (booking == null)
            {
                return Json(new { success = false, message = "Không tìm thấy booking." });
            }

            if (!ActiveHotelStatuses.Contains(booking.Status))
            {
                return Json(new { success = false, message = "Chỉ có thể trả chuồng cho lượt lưu trú đang hoạt động." });
            }

            var checkout = booking.CheckoutStatement;
            if (checkout?.OrderId == null)
            {
                return Json(new { success = false, message = "Booking chưa được chốt chi phí hoặc chưa được thu ngân tạo hóa đơn." });
            }

            if (checkout.Order?.Status != "Chờ xử lý" && checkout.Order?.Status != "Đã thanh toán")
            {
                return Json(new { success = false, message = "Hóa đơn Hotel chưa thanh toán thành công." });
            }

            booking.Status = "Đã trả";
            booking.ScheduledCheckInDate ??= booking.CheckInDate;
            booking.ScheduledCheckOutDate ??= booking.CheckOutDate;
            booking.ActualCheckInAt ??= booking.CheckInDate;
            booking.ActualCheckOutAt = checkout.CheckoutAt;
            booking.CheckOutDate = booking.ActualCheckOutAt;

            var staff = GetCurrentStaffSnapshot();
            _context.PetBioTimelines.Add(new PetBioTimeline
            {
                PetId = booking.PetId,
                HotelBookingId = booking.HotelBookingId,
                Date = booking.ActualCheckOutAt.Value,
                Title = "Hoàn tất lưu trú",
                Type = "HotelCheckOut",
                Description = $"Thú cưng được trả cho chủ nuôi. Chuồng cuối cùng: {booking.CageId}. Nhân viên: {staff.Name}."
            });

            if (booking.Cage != null)
            {
                booking.Cage.Status = "Đang dọn dẹp";
            }

            checkout.Status = "Paid";
            checkout.PaidAt = DateTime.Now;

            await _context.SaveChangesAsync();

            return Json(new { success = true, message = $"Đã hoàn tất trả {booking.Pet?.Name ?? "thú cưng"}; chuồng chuyển sang chờ dọn dẹp." });
        }

        [HttpGet("HotelCheckoutPreview/{bookingId:int}")]
        public async Task<IActionResult> HotelCheckoutPreview(int bookingId)
        {
            var preview = await _hotelCheckoutService.GetPreviewAsync(bookingId);
            return preview == null
                ? Json(new { success = false, message = "Không tìm thấy booking Hotel." })
                : Json(new { success = true, data = preview });
        }

        [HttpPost("PrepareHotelCheckout")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PrepareHotelCheckout(PrepareHotelCheckoutRequest request)
        {
            if (!ModelState.IsValid)
            {
                return Json(new { success = false, message = "Chi phí phát sinh không hợp lệ." });
            }

            try
            {
                var staff = GetCurrentStaffSnapshot();
                var preview = await _hotelCheckoutService.PrepareAsync(request, staff.UserId, staff.Name);
                return Json(new { success = true, message = "Đã chốt chi phí và gửi sang quầy thu ngân.", data = preview });
            }
            catch (InvalidOperationException ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost("CancelOnlineHotelBooking")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelOnlineHotelBooking(int bookingId)
        {
            var booking = await _context.HotelBookings
                .Include(b => b.Pet)
                .FirstOrDefaultAsync(b => b.HotelBookingId == bookingId);

            if (booking == null)
            {
                return Json(new { success = false, message = "Không tìm thấy lịch đặt online." });
            }

            if (!string.Equals(booking.Status, "Đã đặt", StringComparison.OrdinalIgnoreCase))
            {
                return Json(new { success = false, message = "Chỉ có thể hủy lịch đặt online đang chờ tiếp nhận." });
            }

            booking.Status = "Đã hủy";
            var staff = GetCurrentStaffSnapshot();
            _context.PetBioTimelines.Add(new PetBioTimeline
            {
                PetId = booking.PetId,
                HotelBookingId = booking.HotelBookingId,
                Date = DateTime.Now,
                Title = "Hủy lịch lưu trú",
                Type = "HotelBookingCancelled",
                Description = $"Lịch đặt online được hủy bởi {staff.Name}."
            });
            await _context.SaveChangesAsync();

            return Json(new
            {
                success = true,
                message = $"Đã hủy lịch đặt online của {booking.Pet?.Name ?? "thú cưng"}."
            });
        }

        // =========================================================================
        // 6.2. SƠ ĐỒ CHUỒNG TRỰC QUAN & VẬN HÀNH CHUỒNG
        // =========================================================================

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

        [HttpPost("MovePetCage")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MovePetCage(int bookingId, string targetCageId)
        {
            if (bookingId <= 0 || string.IsNullOrWhiteSpace(targetCageId))
            {
                return Json(new { success = false, message = "Thông tin chuyển chuồng không hợp lệ." });
            }

            targetCageId = targetCageId.Trim().ToUpperInvariant();
            await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            try
            {
                var booking = await _context.HotelBookings
                    .Include(b => b.Cage)
                    .Include(b => b.Pet)
                    .FirstOrDefaultAsync(b =>
                        b.HotelBookingId == bookingId &&
                        ActiveHotelStatuses.Contains(b.Status));

                if (booking == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy lượt lưu trú đang hoạt động." });
                }

                string sourceCageId = booking.CageId;
                if (string.Equals(sourceCageId, targetCageId, StringComparison.OrdinalIgnoreCase))
                {
                    return Json(new { success = false, message = "Thú cưng đang ở chuồng này." });
                }

                var targetCage = await _context.Cages
                    .Include(c => c.RoomType)
                    .FirstOrDefaultAsync(c => c.CageId == targetCageId);

                if (targetCage == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy chuồng đích." });
                }

                if (targetCage.Status != "Trống")
                {
                    return Json(new { success = false, message = $"Chuồng {targetCageId} không còn trống." });
                }

                if (!targetCage.RoomType.Status)
                {
                    return Json(new { success = false, message = "Loại chuồng đích đang ngừng hoạt động." });
                }

                bool targetHasConflict = await _context.HotelBookings.AnyAsync(b =>
                    b.CageId == targetCageId &&
                    b.HotelBookingId != booking.HotelBookingId &&
                    BlockingHotelStatuses.Contains(b.Status) &&
                    (!booking.CheckOutDate.HasValue || b.CheckInDate < booking.CheckOutDate.Value) &&
                    (!b.CheckOutDate.HasValue || b.CheckOutDate.Value > booking.CheckInDate));

                if (targetHasConflict)
                {
                    return Json(new { success = false, message = $"Chuồng {targetCageId} đã có lịch đặt trùng thời gian lưu trú." });
                }

                if (booking.Cage != null)
                {
                    booking.Cage.Status = "Trống";
                }

                booking.CageId = targetCage.CageId;
                booking.Cage = targetCage;
                targetCage.Status = "Đang dùng";

                string staffName = User.FindFirst("FullName")?.Value ?? User.Identity?.Name ?? "Nhân viên dịch vụ";
                _context.PetBioTimelines.Add(new PetBioTimeline
                {
                    PetId = booking.PetId,
                    HotelBookingId = booking.HotelBookingId,
                    Date = DateTime.Now,
                    Title = "Chuyển chuồng lưu trú",
                    Type = "HotelCageMove",
                    Description = $"Chuyển từ chuồng {sourceCageId} sang {targetCageId}. Nhân viên: {staffName}."
                });

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Json(new
                {
                    success = true,
                    message = $"Đã chuyển {booking.Pet.Name} từ chuồng {sourceCageId} sang {targetCageId}."
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Không thể chuyển HotelBooking {BookingId} sang chuồng {TargetCageId}.", bookingId, targetCageId);
                return Json(new { success = false, message = "Không thể chuyển chuồng do lỗi hệ thống. Vui lòng thử lại." });
            }
        }

        [HttpPost("UpdateCageOperationalStatus")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateCageOperationalStatus(string cageId, string status, string? reason, string? note)
        {
            if (string.IsNullOrWhiteSpace(cageId) || string.IsNullOrWhiteSpace(status))
            {
                return Json(new { success = false, message = "Thông tin trạng thái chuồng không hợp lệ." });
            }

            cageId = cageId.Trim().ToUpperInvariant();
            status = status.Trim();
            reason = reason?.Trim();
            note = note?.Trim();

            if (!EditableCageStatuses.Contains(status))
            {
                return Json(new { success = false, message = "Chỉ được cập nhật Trống, Đang dọn dẹp, Bảo trì hoặc Khóa." });
            }

            bool isMaintenanceStatus = MaintenanceCageStatuses.Contains(status);
            if (isMaintenanceStatus && string.IsNullOrWhiteSpace(reason))
            {
                return Json(new { success = false, message = "Vui lòng nhập lý do trước khi đưa chuồng vào dọn dẹp, bảo trì hoặc khóa." });
            }

            if (isMaintenanceStatus && reason!.Length < 5)
            {
                return Json(new { success = false, message = "Lý do cần có ít nhất 5 ký tự để đủ rõ ràng cho lịch sử bảo trì." });
            }

            if (!string.IsNullOrWhiteSpace(reason) && reason.Length > 500)
            {
                return Json(new { success = false, message = "Lý do không được vượt quá 500 ký tự." });
            }

            if (!string.IsNullOrWhiteSpace(note) && note.Length > 1000)
            {
                return Json(new { success = false, message = "Ghi chú không được vượt quá 1000 ký tự." });
            }

            await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            try
            {
                var cage = await _context.Cages
                    .Include(c => c.RoomType)
                    .FirstOrDefaultAsync(c => c.CageId == cageId);
                if (cage == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy chuồng." });
                }

                bool hasActivePet = await _context.HotelBookings.AnyAsync(b =>
                    b.CageId == cageId && ActiveHotelStatuses.Contains(b.Status));
                if (hasActivePet)
                {
                    return Json(new { success = false, message = "Không thể đổi trạng thái vận hành khi chuồng đang có thú cưng." });
                }

                if (status == "Trống" && !cage.RoomType.Status)
                {
                    return Json(new { success = false, message = "Không thể mở lại chuồng khi loại chuồng đang ngừng hoạt động." });
                }

                bool hasUpcomingReservation = status != "Trống" && await _context.HotelBookings.AnyAsync(b =>
                    b.CageId == cageId &&
                    b.Status == "Đã đặt" &&
                    (!b.CheckOutDate.HasValue || b.CheckOutDate.Value >= DateTime.Now));
                if (hasUpcomingReservation)
                {
                    return Json(new
                    {
                        success = false,
                        message = "Chuồng đang có lịch đặt online sắp tới. Hãy xử lý lịch đặt trước khi đưa chuồng vào dọn dẹp, bảo trì hoặc khóa."
                    });
                }

                var openMaintenanceLog = await _context.RoomMaintenanceLogs
                    .Where(log => log.CageId == cageId && log.EndedAt == null)
                    .OrderByDescending(log => log.StartedAt)
                    .FirstOrDefaultAsync();

                var actor = GetCurrentStaffSnapshot();
                DateTime now = DateTime.Now;

                if (status == "Trống")
                {
                    if (openMaintenanceLog != null)
                    {
                        openMaintenanceLog.EndedAt = now;
                        openMaintenanceLog.EndedByUserId = actor.UserId;
                        openMaintenanceLog.EndedByName = actor.Name;
                        if (!string.IsNullOrWhiteSpace(note))
                        {
                            openMaintenanceLog.Note = note;
                        }
                    }

                    cage.Status = status;
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();
                    return Json(new { success = true, message = $"Đã mở lại chuồng {cageId} và ghi nhận thời gian kết thúc bảo trì/khóa nếu có." });
                }

                if (openMaintenanceLog != null && !string.Equals(openMaintenanceLog.NewStatus, status, StringComparison.OrdinalIgnoreCase))
                {
                    openMaintenanceLog.EndedAt = now;
                    openMaintenanceLog.EndedByUserId = actor.UserId;
                    openMaintenanceLog.EndedByName = actor.Name;
                    if (!string.IsNullOrWhiteSpace(note))
                    {
                        openMaintenanceLog.Note = note;
                    }

                    await _context.SaveChangesAsync();
                    openMaintenanceLog = null;
                }

                if (openMaintenanceLog == null)
                {
                    _context.RoomMaintenanceLogs.Add(new RoomMaintenanceLog
                    {
                        CageId = cage.CageId,
                        PreviousStatus = cage.Status,
                        NewStatus = status,
                        Reason = reason!,
                        Note = string.IsNullOrWhiteSpace(note) ? null : note,
                        StartedAt = now,
                        CreatedByUserId = actor.UserId,
                        CreatedByName = actor.Name
                    });
                }
                else
                {
                    openMaintenanceLog.Reason = reason!;
                    if (!string.IsNullOrWhiteSpace(note))
                    {
                        openMaintenanceLog.Note = note;
                    }
                }

                cage.Status = status;
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                return Json(new { success = true, message = $"Đã cập nhật chuồng {cageId} sang trạng thái {status} và ghi nhận lịch sử bảo trì." });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Không thể cập nhật trạng thái vận hành cho chuồng {CageId}.", cageId);
                return Json(new { success = false, message = "Không thể cập nhật trạng thái chuồng do lỗi hệ thống." });
            }
        }

        // =========================================================================
        // 6.3. CRUD LOẠI CHUỒNG (ROOM TYPE)
        // =========================================================================

        private static string? ValidateRoomTypePricing(decimal dailyPrice, decimal hourlyPrice)
        {
            if (dailyPrice < MinimumRoomTypeDailyPrice)
            {
                return "Giá theo ngày không được thấp hơn 150.000đ.";
            }

            if (hourlyPrice < MinimumRoomTypeHourlyPrice)
            {
                return "Giá theo giờ không được thấp hơn 40.000đ.";
            }

            return null;
        }

        [HttpPost("AddRoomType")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddRoomType(
            string type, string size, int capacity,
            decimal hourlyPrice, decimal dailyPrice,
            bool hasAc, bool hasCamera, bool hasPremiumFood)
        {
            if (string.IsNullOrWhiteSpace(type) || capacity <= 0)
            {
                TempData["HotelError"] = "Thông tin loại chuồng không hợp lệ.";
                return RedirectToAction(nameof(CageCategories));
            }

            var pricingError = ValidateRoomTypePricing(dailyPrice, hourlyPrice);
            if (pricingError != null)
            {
                TempData["HotelError"] = pricingError;
                return RedirectToAction(nameof(CageCategories));
            }

            if (await _context.RoomTypes.AnyAsync(r => r.Type.ToLower() == type.Trim().ToLower()))
            {
                TempData["HotelError"] = "Tên loại chuồng này đã tồn tại.";
                return RedirectToAction(nameof(CageCategories));
            }

            var roomType = new RoomType
            {
                Type = type.Trim(),
                Size = size?.Trim() ?? "Tiêu chuẩn",
                Capacity = capacity,
                HourlyPrice = hourlyPrice,
                DailyPrice = dailyPrice,
                HasAc = hasAc,
                HasCamera = hasCamera,
                HasPremiumFood = hasPremiumFood,
                Status = true
            };

            _context.RoomTypes.Add(roomType);
            await _context.SaveChangesAsync();

            TempData["HotelSuccess"] = $"Thêm loại chuồng '{type}' thành công!";
            return RedirectToAction(nameof(CageCategories));
        }

        [HttpPost("EditRoomType")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditRoomType(
            int id, string type, string size, int capacity,
            decimal hourlyPrice, decimal dailyPrice,
            bool hasAc, bool hasCamera, bool hasPremiumFood)
        {
            var roomType = await _context.RoomTypes.FindAsync(id);
            if (roomType == null)
            {
                TempData["HotelError"] = "Không tìm thấy loại chuồng.";
                return RedirectToAction(nameof(CageCategories));
            }

            if (string.IsNullOrWhiteSpace(type) || capacity <= 0)
            {
                TempData["HotelError"] = "Thông tin không hợp lệ.";
                return RedirectToAction(nameof(CageCategories));
            }

            var pricingError = ValidateRoomTypePricing(dailyPrice, hourlyPrice);
            if (pricingError != null)
            {
                TempData["HotelError"] = pricingError;
                return RedirectToAction(nameof(CageCategories));
            }

            if (await _context.RoomTypes.AnyAsync(r => r.Type.ToLower() == type.Trim().ToLower() && r.RoomTypeId != id))
            {
                TempData["HotelError"] = "Tên loại chuồng này đã tồn tại.";
                return RedirectToAction(nameof(CageCategories));
            }

            roomType.Type = type.Trim();
            roomType.Size = size?.Trim() ?? "Tiêu chuẩn";
            roomType.Capacity = capacity;
            roomType.HourlyPrice = hourlyPrice;
            roomType.DailyPrice = dailyPrice;
            roomType.HasAc = hasAc;
            roomType.HasCamera = hasCamera;
            roomType.HasPremiumFood = hasPremiumFood;

            await _context.SaveChangesAsync();
            TempData["HotelSuccess"] = "Cập nhật loại chuồng thành công!";
            return RedirectToAction(nameof(CageCategories));
        }

        [HttpPost("DeleteRoomType")]
        public async Task<IActionResult> DeleteRoomType(int id)
        {
            var roomType = await _context.RoomTypes.FindAsync(id);
            if (roomType == null)
                return Json(new { success = false, message = "Không tìm thấy loại chuồng." });

            bool hasCages = await _context.Cages.AnyAsync(c => c.RoomTypeId == id);
            bool hasOrders = await _context.OrderItems.AnyAsync(o => o.RoomTypeId == id);

            if (hasCages || hasOrders)
            {
                roomType.Status = false;
                await _context.SaveChangesAsync();
                return Json(new { success = true, isSoftDeleted = true, message = "Loại chuồng đang được sử dụng, đã tự động chuyển sang trạng thái Ngưng hoạt động!" });
            }

            _context.RoomTypes.Remove(roomType);
            await _context.SaveChangesAsync();
            return Json(new { success = true, isSoftDeleted = false, message = "Xóa loại chuồng thành công!" });
        }

        [HttpPost("ToggleRoomType")]
        public async Task<IActionResult> ToggleRoomType(int id)
        {
            var roomType = await _context.RoomTypes.FindAsync(id);
            if (roomType == null)
                return Json(new { success = false, message = "Không tìm thấy loại chuồng." });

            roomType.Status = !roomType.Status;
            await _context.SaveChangesAsync();
            return Json(new { success = true, status = roomType.Status });
        }

        // =========================================================================
        // 6.4. CRUD CHUỒNG (CAGE)
        // =========================================================================

        [HttpPost("AddCage")]
        public async Task<IActionResult> AddCage(
            string cageId, int roomTypeId, string feedSchedule, int portion)
        {
            if (string.IsNullOrWhiteSpace(cageId) || roomTypeId <= 0)
            {
                TempData["HotelError"] = "Thông tin chuồng không hợp lệ.";
                return RedirectToAction(nameof(CageCategories));
            }

            if (await _context.Cages.AnyAsync(c => c.CageId == cageId.Trim().ToUpper()))
            {
                TempData["HotelError"] = $"Mã chuồng '{cageId}' đã tồn tại.";
                return RedirectToAction(nameof(CageCategories));
            }

            var roomType = await _context.RoomTypes.FindAsync(roomTypeId);
            if (roomType == null)
            {
                TempData["HotelError"] = "Loại chuồng không tồn tại.";
                return RedirectToAction(nameof(CageCategories));
            }

            var cage = new Cage
            {
                CageId = cageId.Trim().ToUpper(),
                RoomTypeId = roomTypeId,
                Status = "Trống",
                FeedSchedule = feedSchedule?.Trim() ?? "08:00, 12:00, 18:00",
                Portion = portion > 0 ? portion : 60
            };

            _context.Cages.Add(cage);
            await _context.SaveChangesAsync();

            TempData["HotelSuccess"] = $"Thêm chuồng {cage.CageId} thành công!";
            return RedirectToAction(nameof(CageCategories));
        }

        [HttpPost("EditCage")]
        public async Task<IActionResult> EditCage(
            string cageId, int roomTypeId, string feedSchedule, int portion)
        {
            var cage = await _context.Cages.FindAsync(cageId);
            if (cage == null)
            {
                TempData["HotelError"] = "Không tìm thấy chuồng.";
                return RedirectToAction(nameof(CageCategories));
            }

            var roomType = await _context.RoomTypes.FindAsync(roomTypeId);
            if (roomType == null)
            {
                TempData["HotelError"] = "Loại chuồng không tồn tại.";
                return RedirectToAction(nameof(CageCategories));
            }

            cage.RoomTypeId = roomTypeId;
            cage.FeedSchedule = feedSchedule?.Trim() ?? cage.FeedSchedule;
            cage.Portion = portion > 0 ? portion : cage.Portion;

            await _context.SaveChangesAsync();
            TempData["HotelSuccess"] = $"Cập nhật chuồng {cageId} thành công!";
            return RedirectToAction(nameof(CageCategories));
        }

        [HttpPost("DeleteCage")]
        public async Task<IActionResult> DeleteCage(string cageId)
        {
            var cage = await _context.Cages.FindAsync(cageId);
            if (cage == null)
                return Json(new { success = false, message = "Không tìm thấy chuồng." });

            if (cage.Status != "Trống")
                return Json(new { success = false, message = "Không thể xóa chuồng đang có thú cưng." });

            bool hasBookings = await _context.HotelBookings.AnyAsync(b => b.CageId == cageId);
            if (hasBookings)
                return Json(new { success = false, message = "Chuồng này có lịch sử booking, không thể xóa." });

            _context.Cages.Remove(cage);
            await _context.SaveChangesAsync();
            return Json(new { success = true, message = $"Xóa chuồng {cageId} thành công!" });
        }

        [HttpGet("MedicalRecords")]
        public async Task<IActionResult> MedicalRecords(string? species)
        {
            ViewBag.SelectedSpecies = species;
            
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
