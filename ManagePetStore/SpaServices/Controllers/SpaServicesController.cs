using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ManagePetStore.Models;
using ManagePetStore.SpaServices.Models;

namespace ManagePetStore.SpaServices.Controllers
{
    [Authorize(Roles = "service,admin")]
    [Route("SpaServices")]
    public class SpaServicesController : Controller
    {
        private static readonly string[] ActiveHotelStatuses = ["Active", "Đang ở"];
        private static readonly string[] BlockingHotelStatuses = ["Đã đặt", "Active", "Đang ở"];
        private static readonly string[] EditableCageStatuses = ["Trống", "Đang dọn dẹp", "Bảo trì", "Khóa"];

        private readonly PetStoreManagementContext _context;
        private readonly ILogger<SpaServicesController> _logger;

        public SpaServicesController(PetStoreManagementContext context, ILogger<SpaServicesController> logger)
        {
            _context = context;
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

            return View("~/SpaServices/Views/SpaService/Index.cshtml");
        }

        // =========================================================================
        // 2. PHÂN HỆ 4.1: QUẢN LÝ DANH MỤC DỊCH VỤ SPA (CRUD & TOGGLE ACTIVE)
        // =========================================================================
        
        [HttpPost("AddService")]
        public async Task<IActionResult> AddService(string name, int duration, decimal price)
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
                Active = true
            };

            _context.SpaServices.Add(service);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Thêm dịch vụ Spa mới thành công!";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost("EditService")]
        public async Task<IActionResult> EditService(int id, string name, int duration, decimal price)
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
                    var customer = await _context.Customers.FirstOrDefaultAsync(c => c.Phone == phone.Trim());
                    if (customer == null)
                    {
                        customer = new ManagePetStore.Models.Customer
                        {
                            FullName = customerName.Trim(),
                            Phone = phone.Trim(),
                            CreatedAt = DateTime.Now,
                            MembershipTier = "Bronze"
                        };
                        _context.Customers.Add(customer);
                        await _context.SaveChangesAsync();
                    }

                    var pet = new Pet
                    {
                        CustomerId = customer.CustomerId,
                        Name = petName.Trim(),
                        Species = species ?? "Chó",
                        Breed = breed?.Trim() ?? "Không rõ",
                        Age = age?.Trim() ?? "Chưa rõ",
                        Weight = weight > 0 ? weight : 4.5m,
                        Status = "Active"
                    };
                    _context.Pets.Add(pet);
                    await _context.SaveChangesAsync();

                    int countToday = await _context.SpaQueues.CountAsync(q => q.QueueNumber.StartsWith("WI-") || q.QueueNumber.StartsWith("PEND-WI-"));
                    string queueNumber = $"PEND-WI-{(700 + countToday + 1)}";

                    DateTime arrivalTime = DateTime.Now;
                    if (!string.IsNullOrEmpty(timeSlot) && TimeSpan.TryParse(timeSlot, out TimeSpan ts))
                    {
                        arrivalTime = DateTime.Today.Add(ts);
                    }
                    redirectDate = arrivalTime.ToString("yyyy-MM-dd");

                    var queueItem = new SpaQueue
                    {
                        QueueNumber = queueNumber,
                        PetName = pet.Name,
                        OwnerName = $"{customer.FullName} ({customer.Phone})",
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

            var groomer = await _context.Users.FindAsync(groomerId);
            if (groomer == null)
            {
                return Json(new { success = false, message = "Không tìm thấy kỹ thuật viên." });
            }

            var ownerName = queueItem.OwnerName;
            Customer? customer = null;
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

            // Kiểm tra trùng lịch của Groomer tại khung giờ này (áp dụng cho cả online và offline)
            bool isOverlap = await _context.SpaBookings
                .AnyAsync(b => b.GroomerId == groomerId && b.DateTime == targetBookingDateTime && b.SpaStatus != "Cancelled");

            if (isOverlap)
            {
                return Json(new { success = false, message = $"Kỹ thuật viên {groomer.FullName} đã có ca làm việc vào lúc {targetBookingDateTime:HH:mm}. Vui lòng chọn Kỹ thuật viên khác!" });
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

            // Nếu nhân viên hoàn thành trạng thái chăm sóc thú cưng thì lưu vào bảng StaffTasks
            if (status == "Hoàn thành")
            {
                await _context.Entry(booking).Reference(b => b.Pet).LoadAsync();
                await _context.Entry(booking).Reference(b => b.Customer).LoadAsync();
                await _context.Entry(booking).Reference(b => b.Service).LoadAsync();

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

        // =========================================================================
        // 5. ĐẶT LỊCH SPA NHANH & DỮ LIỆU ĐỘNG
        // =========================================================================
        
        [HttpPost("CreateQuickBooking")]
        public async Task<IActionResult> CreateQuickBooking(int groomerId, string date, string time, int customerId, int petId, int serviceId, string notes)
        {
            var customer = await _context.Customers.FindAsync(customerId);
            var pet = await _context.Pets.FindAsync(petId);
            var service = await _context.SpaServices.FindAsync(serviceId);
            var groomer = await _context.Users.FindAsync(groomerId);

            if (customer == null || pet == null || service == null || !service.Active || groomer == null)
            {
                TempData["ErrorMessage"] = "Thông tin không hợp lệ.";
                return RedirectToAction(nameof(Index));
            }

            if (!DateTime.TryParse($"{date} {time}", out DateTime bookingDateTime))
            {
                bookingDateTime = DateTime.Today.AddHours(12);
            }

            bool isOverlap = await _context.SpaBookings
                .AnyAsync(b => b.GroomerId == groomerId && b.DateTime == bookingDateTime && b.SpaStatus != "Cancelled");

            if (isOverlap)
            {
                TempData["ErrorMessage"] = $"Groomer {groomer.FullName} đã có lịch vào khung giờ {time} ngày {date}.";
                return RedirectToAction(nameof(Index));
            }

            var booking = new SpaBooking
            {
                CustomerId = customer.CustomerId,
                PetId = pet.PetId,
                ServiceId = service.ServiceId,
                GroomerId = groomerId,
                DateTime = bookingDateTime,
                Price = service.Price,
                Status = "Chưa thanh toán",
                SpaStatus = "|0", // Bắt đầu ở bước 0 (Tiếp nhận)
                Notes = notes
            };

            _context.SpaBookings.Add(booking);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Đặt lịch thành công cho pet {pet.Name} lúc {time}!";
            return RedirectToAction(nameof(Index), new { date = date });
        }

        [HttpPost("CancelBooking")]
        public async Task<IActionResult> CancelBooking(int bookingId)
        {
            var booking = await _context.SpaBookings.FindAsync(bookingId);
            if (booking == null)
            {
                return Json(new { success = false, message = "Không tìm thấy lịch hẹn." });
            }

            _context.SpaBookings.Remove(booking);
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Hủy lịch hẹn thành công!" });
        }

        [HttpGet("GetPetsByCustomer")]
        public async Task<IActionResult> GetPetsByCustomer(int customerId)
        {
            var pets = await _context.Pets
                .Where(p => p.CustomerId == customerId && p.Status == "Active")
                .Select(p => new { petId = p.PetId, name = p.Name, species = p.Species, breed = p.Breed })
                .ToListAsync();

            return Json(pets);
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
                    if (queueItem.QueueNumber.StartsWith("OL-"))
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

        [HttpGet("Hotel")]
        public IActionResult Hotel()
        {
            return RedirectToAction(nameof(CageMap));
        }

        [HttpGet("PetCheckIn")]
        public Task<IActionResult> PetCheckIn(int roomTypePage = 1, int cagePage = 1)
        {
            return HotelWorkspace("checkin", roomTypePage, cagePage);
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

            // Danh sách Customers cho dropdown
            var customers = await _context.Customers
                .Include(c => c.Pets)
                .OrderBy(c => c.FullName)
                .ToListAsync();
            ViewBag.Customers = customers;

            return View("~/SpaServices/Views/SpaService/Hotel.cshtml");
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
                    pathology = p.Pathology ?? ""
                })
                .ToListAsync();
            return Json(pets);
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
                }
                else
                {
                    if (customer == null)
                    {
                        customer = new Customer
                        {
                            FullName = request.CustomerName.Trim(),
                            Phone = customerPhone,
                            CreatedAt = DateTime.Now,
                            MembershipTier = "Bronze"
                        };
                        _context.Customers.Add(customer);
                        await _context.SaveChangesAsync();
                    }
                    else if (await _context.Pets.AnyAsync(p => p.CustomerId == customer.CustomerId && p.Name == petName))
                    {
                        return HotelValidationError($"Hồ sơ thú cưng '{petName}' đã tồn tại. Vui lòng chọn hồ sơ có sẵn.");
                    }

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

                bool petIsAlreadyBoarding = await _context.HotelBookings.AnyAsync(b =>
                    b.PetId == pet.PetId && (b.Status == "Active" || b.Status == "Đang ở"));
                if (petIsAlreadyBoarding)
                {
                    return HotelValidationError($"{pet.Name} đang có một lượt lưu trú chưa hoàn tất.");
                }

                var onlineReservation = await _context.HotelBookings.FirstOrDefaultAsync(b =>
                    b.PetId == pet.PetId &&
                    b.CustomerId == customer.CustomerId &&
                    b.Status == "Đã đặt" &&
                    b.CheckInDate.Date == checkInDate.Date);

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

                // Ghi nhận sức khỏe trước khi tạo booking và đổi trạng thái chuồng.
                _context.PetBioTimelines.Add(new PetBioTimeline
                {
                    PetId = pet.PetId,
                    Date = DateTime.Now,
                    Title = "Kiểm tra sức khỏe đầu vào",
                    Type = "HealthCheckIn",
                    Description = BuildHealthCheckDescription(request, pathology, healthNote)
                });

                _context.PetBioTimelines.Add(new PetBioTimeline
                {
                    PetId = pet.PetId,
                    Date = DateTime.Now,
                    Title = "Pet Check-In lưu trú",
                    Type = "PetCheckIn",
                    Description = BuildPetCheckInDescription(request, cageId, customer.FullName)
                });

                decimal dailyPrice = cage.RoomType.DailyPrice;
                int stayDays = checkOutDate.HasValue
                    ? Math.Max(1, (int)Math.Ceiling((checkOutDate.Value - checkInDate).TotalDays))
                    : 1;
                decimal subtotal = dailyPrice * stayDays;

                if (onlineReservation != null)
                {
                    onlineReservation.CheckInDate = checkInDate;
                    onlineReservation.Status = "Đang ở";
                }
                else
                {
                    _context.HotelBookings.Add(new HotelBooking
                    {
                        CageId = cageId,
                        PetId = pet.PetId,
                        CustomerId = customer.CustomerId,
                        CheckInDate = checkInDate,
                        CheckOutDate = checkOutDate,
                        StayDays = stayDays,
                        BaseDailyPrice = dailyPrice,
                        Subtotal = subtotal,
                        Discount = 0,
                        FinalAmount = subtotal,
                        EarnedPoints = 0,
                        Status = "Đang ở"
                    });
                }

                cage.Status = "Đang dùng";

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                TempData["HotelSuccess"] = $"Đã ghi nhận sức khỏe và tiếp nhận {pet.Name} vào chuồng {cageId} thành công!";
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

            return RedirectToAction(nameof(PetCheckIn));
        }

        private IActionResult HotelValidationError(string message)
        {
            TempData["HotelError"] = message;
            return RedirectToAction(nameof(PetCheckIn));
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

            return $"Kết luận: {conclusion}\n"
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

            return $"Chuồng tiếp nhận: {cageId}\n"
                 + $"Chủ thú cưng: {customerName}\n"
                 + $"Ngày nhận: {request.CheckInDate!.Value:dd/MM/yyyy HH:mm}\n"
                 + $"Ngày trả dự kiến: {expectedCheckout}\n"
                 + $"Nhân viên tiếp nhận: {checkedBy}";
        }

        [HttpPost("CheckOut")]
        public async Task<IActionResult> CheckOut(int bookingId)
        {
            var booking = await _context.HotelBookings
                .Include(b => b.Cage)
                .Include(b => b.Pet)
                .FirstOrDefaultAsync(b => b.HotelBookingId == bookingId);

            if (booking == null)
            {
                return Json(new { success = false, message = "Không tìm thấy booking." });
            }

            booking.Status = "Đã trả";
            booking.CheckOutDate = DateTime.Now;

            if (booking.Cage != null)
            {
                booking.Cage.Status = "Trống";
            }

            await _context.SaveChangesAsync();

            return Json(new { success = true, message = $"Đã trả chuồng cho {booking.Pet?.Name ?? "thú cưng"}!" });
        }

        // =========================================================================
        // 6.2. SƠ ĐỒ CHUỒNG TRỰC QUAN & VẬN HÀNH CHUỒNG
        // =========================================================================

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
        public async Task<IActionResult> UpdateCageOperationalStatus(string cageId, string status)
        {
            if (string.IsNullOrWhiteSpace(cageId) || string.IsNullOrWhiteSpace(status))
            {
                return Json(new { success = false, message = "Thông tin trạng thái chuồng không hợp lệ." });
            }

            cageId = cageId.Trim().ToUpperInvariant();
            status = status.Trim();

            if (!EditableCageStatuses.Contains(status))
            {
                return Json(new { success = false, message = "Chỉ được cập nhật Trống, Đang dọn dẹp, Bảo trì hoặc Khóa." });
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

                cage.Status = status;
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                return Json(new { success = true, message = $"Đã cập nhật chuồng {cageId} sang trạng thái {status}." });
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

        [HttpPost("AddRoomType")]
        public async Task<IActionResult> AddRoomType(
            string type, string size, int capacity,
            decimal hourlyPrice, decimal dailyPrice,
            bool hasAc, bool hasCamera, bool hasPremiumFood)
        {
            if (string.IsNullOrWhiteSpace(type) || capacity <= 0 || dailyPrice < 0)
            {
                TempData["HotelError"] = "Thông tin loại chuồng không hợp lệ.";
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

            if (string.IsNullOrWhiteSpace(type) || capacity <= 0 || dailyPrice < 0)
            {
                TempData["HotelError"] = "Thông tin không hợp lệ.";
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
    }
}
