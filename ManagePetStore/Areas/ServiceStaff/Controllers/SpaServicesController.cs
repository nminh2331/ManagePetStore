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
    public class SpaServicesController : Controller
    {
        private static readonly string[] SpaProgressStatuses = ["Tiếp nhận", "Tắm & Sấy", "Cắt & Tỉa", "Massage", "Hoàn thành"];

        private static string ExtractPhoneFromOwnerName(string ownerName)
        {
            if (string.IsNullOrEmpty(ownerName)) return "";
            if (ownerName.Contains("(") && ownerName.Contains(")"))
            {
                int startIndex = ownerName.LastIndexOf("(") + 1;
                int endIndex = ownerName.LastIndexOf(")");
                if (startIndex > 0 && endIndex > startIndex)
                {
                    return ownerName.Substring(startIndex, endIndex - startIndex).Trim();
                }
            }
            return "";
        }

        private readonly PetStoreManagementContext _context;
        private readonly IHotelBookingHistoryService _historyService;
        private readonly IHotelCareMediaService _hotelCareMediaService;
        private readonly IHubContext<HotelCareHub> _hotelCareHub;
        private readonly IHotelCheckoutService _hotelCheckoutService;
        private readonly IInventoryBatchService _inventoryBatchService;
        private readonly IHotelEmailService _hotelEmailService;
        private readonly ILogger<SpaServicesController> _logger;

        public SpaServicesController(
            PetStoreManagementContext context,
            IHotelBookingHistoryService historyService,
            IHotelCareMediaService hotelCareMediaService,
            IHubContext<HotelCareHub> hotelCareHub,
            IHotelCheckoutService hotelCheckoutService,
            IInventoryBatchService inventoryBatchService,
            IHotelEmailService hotelEmailService,
            ILogger<SpaServicesController> logger)
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

            var services = await _context.SpaServices.AsNoTracking()
                .OrderBy(s => s.ServiceId)
                .Skip((currentPage - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
            
            ViewBag.Services = services;
            ViewBag.CurrentPage = currentPage;
            ViewBag.TotalPages = totalPages;

            // Chỉ lấy dịch vụ đang active cho các Dropdown
            var activeServices = await _context.SpaServices.AsNoTracking()
                .Where(s => s.Active)
                .OrderBy(s => s.ServiceId)
                .ToListAsync();
            ViewBag.ActiveServices = activeServices;

            // Phân hệ 4.2: Phân ca Nhân viên có PHÂN TRANG (PageSize = 3)
            int groomerPageSize = 3;
            int activeGroomersCount = await _context.Users.AsNoTracking()
                .Include(u => u.Role)
                .Where(u => u.Role.RoleName == "service" && u.Status == "Active")
                .CountAsync();
            ViewBag.ActiveGroomersCount = activeGroomersCount;

            int totalGroomerPages = (int)Math.Ceiling((double)activeGroomersCount / groomerPageSize);
            int currentGroomerPage = groomerPage < 1 ? 1 : (groomerPage > totalGroomerPages ? totalGroomerPages : groomerPage);
            if (currentGroomerPage < 1) currentGroomerPage = 1;

            var groomers = await _context.Users.AsNoTracking()
                .Include(u => u.Role)
                .Where(u => u.Role.RoleName == "service" && u.Status == "Active")
                .OrderBy(u => u.UserId)
                .Skip((currentGroomerPage - 1) * groomerPageSize)
                .Take(groomerPageSize)
                .ToListAsync();

            var allGroomers = await _context.Users.AsNoTracking()
                .Include(u => u.Role)
                .Where(u => u.Role.RoleName == "service" && u.Status == "Active")
                .OrderBy(u => u.UserId)
                .ToListAsync();

            ViewBag.Groomers = groomers;
            ViewBag.AllGroomers = allGroomers;
            ViewBag.GroomerPage = currentGroomerPage;
            ViewBag.TotalGroomerPages = totalGroomerPages;

            var bookings = await _context.SpaBookings.AsNoTracking()
                .Include(b => b.Pet)
                .Include(b => b.Customer)
                .Include(b => b.Service)
                .Where(b => b.DateTime.Date == selectedDate.Date)
                .ToListAsync();
            ViewBag.Bookings = bookings;

            // Phân hệ 4.3: Hàng đợi Spa Real-time có PHÂN TRANG (PageSize = 4)
            int queuePageSize = 4;
            int totalQueueItems = await _context.SpaQueues.AsNoTracking().CountAsync(q => !q.QueueNumber.StartsWith("PEND-WI-"));
            int totalQueuePages = (int)Math.Ceiling((double)totalQueueItems / queuePageSize);
            int currentQueuePage = queuePage < 1 ? 1 : (queuePage > totalQueuePages ? totalQueuePages : queuePage);
            if (currentQueuePage < 1) currentQueuePage = 1;

            var queue = await _context.SpaQueues.AsNoTracking()
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
            var walkInItems = await _context.SpaQueues.AsNoTracking()
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
                string phone = ExtractPhoneFromOwnerName(firstWalkInItem.OwnerName);
                var customer = await _context.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Phone == phone);
                var pet = customer != null ? await _context.Pets.AsNoTracking().FirstOrDefaultAsync(p => p.CustomerId == customer.CustomerId && p.Name == firstWalkInItem.PetName) : null;
                ViewBag.WalkInPet = pet;
            }

            // Dữ liệu dropdowns
            var customers = await _context.Customers.AsNoTracking()
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

                    var allQueueNumbers = await _context.SpaQueues
                        .Where(q => q.QueueNumber.StartsWith("WI-") || q.QueueNumber.StartsWith("PEND-WI-"))
                        .Select(q => q.QueueNumber)
                        .ToListAsync();
                    
                    int maxNum = 700;
                    foreach (var qNum in allQueueNumbers)
                    {
                        string cleanNumStr = qNum.Replace("PEND-WI-", "").Replace("WI-", "");
                        if (int.TryParse(cleanNumStr, out int numVal) && numVal > maxNum)
                        {
                            maxNum = numVal;
                        }
                    }
                    string queueNumber = $"PEND-WI-{(maxNum + 1)}";

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
            string phone = ExtractPhoneFromOwnerName(ownerName);
            if (!string.IsNullOrEmpty(phone))
            {
                customer = await _context.Customers.FirstOrDefaultAsync(c => c.Phone == phone);
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
            var bookedSlotsToday = await _context.SpaBookings.AsNoTracking()
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
            var booking = await _context.SpaBookings.AsNoTracking()
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
            var statuses = SpaProgressStatuses;
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

            var statuses = SpaProgressStatuses;
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
                            PointsEarned = 10,
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

            string phone = ExtractPhoneFromOwnerName(queueItem.OwnerName);
            string customerName = queueItem.OwnerName;
            if (!string.IsNullOrEmpty(phone) && queueItem.OwnerName.Contains("("))
            {
                customerName = queueItem.OwnerName.Substring(0, queueItem.OwnerName.LastIndexOf("(")).Trim();
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
                    string origPhone = ExtractPhoneFromOwnerName(queueItem.OwnerName);

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
                    string phone = ExtractPhoneFromOwnerName(queueItem.OwnerName);
                    string ownerName = queueItem.OwnerName;

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
                                    ?? await _context.Users.FirstOrDefaultAsync(u => u.UserId == 3)
                                    ?? await _context.Users.FirstOrDefaultAsync();
                                if (defaultGroomer == null)
                                {
                                    return Json(new { success = false, message = "Không tìm thấy kỹ thuật viên hợp lệ trong hệ thống để hủy lịch." });
                                }
                                int groomerId = defaultGroomer.UserId;

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
    }
}
