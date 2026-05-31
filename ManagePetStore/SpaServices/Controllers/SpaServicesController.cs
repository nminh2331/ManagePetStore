using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ManagePetStore.Models;

namespace ManagePetStore.SpaServices.Controllers
{
    [Authorize(Roles = "service,admin")]
    [Route("SpaServices")]
    public class SpaServicesController : Controller
    {
        private readonly PetStoreManagementContext _context;

        public SpaServicesController(PetStoreManagementContext context)
        {
            _context = context;
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

            ViewBag.Groomers = groomers;
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
            int totalQueueItems = await _context.SpaQueues.CountAsync();
            int totalQueuePages = (int)Math.Ceiling((double)totalQueueItems / queuePageSize);
            int currentQueuePage = queuePage < 1 ? 1 : (queuePage > totalQueuePages ? totalQueuePages : queuePage);
            if (currentQueuePage < 1) currentQueuePage = 1;

            var queue = await _context.SpaQueues
                .OrderBy(q => q.ArrivalTime)
                .Skip((currentQueuePage - 1) * queuePageSize)
                .Take(queuePageSize)
                .ToListAsync();

            ViewBag.Queue = queue;
            ViewBag.QueuePage = currentQueuePage;
            ViewBag.TotalQueuePages = totalQueuePages;
            ViewBag.TotalQueueItems = totalQueueItems;

            // Khách vãng lai chờ (bắt đầu bằng WI-) có PHÂN TRANG (PageSize = 1)
            var walkInItems = await _context.SpaQueues
                .Where(q => q.QueueNumber.StartsWith("WI-"))
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
                        customer = new Models.Customer
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

                    int countToday = await _context.SpaQueues.CountAsync(q => q.QueueNumber.StartsWith("WI-"));
                    string queueNumber = $"WI-{(700 + countToday + 1)}";

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
        public async Task<IActionResult> StartQueue(int queueId, int groomerId)
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

            // Kiểm tra trùng lịch của Groomer tại khung giờ này
            bool isOverlap = await _context.SpaBookings
                .AnyAsync(b => b.GroomerId == groomerId && b.DateTime == queueItem.ArrivalTime && b.SpaStatus != "Cancelled");

            if (isOverlap)
            {
                return Json(new { success = false, message = $"Kỹ thuật viên {groomer.FullName} đã có ca làm việc vào lúc {queueItem.ArrivalTime:HH:mm}. Vui lòng chọn ca hoặc Kỹ thuật viên khác!" });
            }

            var booking = new SpaBooking
            {
                CustomerId = customer.CustomerId,
                PetId = pet.PetId,
                ServiceId = service.ServiceId,
                GroomerId = groomer.UserId,
                DateTime = queueItem.ArrivalTime, // Dùng đúng khung giờ đã chọn từ trước
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
            int total = await _context.SpaQueues.CountAsync();
            int totalPages = (int)Math.Ceiling((double)total / pageSize);
            int currentPage = page < 1 ? 1 : (page > totalPages ? totalPages : page);
            if (currentPage < 1) currentPage = 1;

            var queue = await _context.SpaQueues
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
                petName = queueItem.PetName,
                species = pet?.Species ?? "Chó",
                breed = pet?.Breed ?? "Không rõ",
                age = pet?.Age ?? "Chưa rõ",
                weight = pet?.Weight ?? 4.5m,
                customerName = customerName,
                phone = phone,
                serviceId = service?.ServiceId ?? 0,
                timeSlot = queueItem.ArrivalTime.ToString("HH:mm"),
                note = queueItem.Note
            });
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
    }
}
