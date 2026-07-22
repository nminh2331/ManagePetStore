using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using ManagePetStore.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PayOS;

namespace ManagePetStore.Areas.Customer.Controllers
{
    [Area("Customer")]
    [Authorize]
    [Route("Customer/SpaBooking")]
    public class SpaBookingController : Controller
    {
        private readonly PetStoreManagementContext _context;
        private readonly PayOSClient _payOS;

        public SpaBookingController(PetStoreManagementContext context, PayOSClient payOS)
        {
            _context = context;
            _payOS = payOS;
        }

        private async Task<ManagePetStore.Models.Customer?> GetCurrentCustomerAsync()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out var userId))
            {
                return null;
            }

            return await _context.Customers.FirstOrDefaultAsync(c => c.UserId == userId);
        }

        [HttpGet("History")]
        public async Task<IActionResult> History(string? searchTerm, string statusFilter = "all", int page = 1)
        {
            var layout = await BuildSidebarViewModelAsync("spabooking");
            if (layout == null)
            {
                return RedirectToAction("Login", "Account", new { area = "Customer" });
            }

            var query = _context.SpaBookings
                .AsNoTracking()
                .Include(b => b.Pet)
                .Include(b => b.Service)
                .Include(b => b.Groomer)
                .Where(b => b.CustomerId == layout.Customer.CustomerId);

            var normalizedSearch = searchTerm?.Trim() ?? "";
            if (!string.IsNullOrWhiteSpace(normalizedSearch))
            {
                var term = normalizedSearch.ToLower();
                query = query.Where(b =>
                    b.BookingId.ToString().Contains(term) ||
                    (b.Service != null && b.Service.Name.ToLower().Contains(term)) ||
                    (b.Pet != null && b.Pet.Name.ToLower().Contains(term)) ||
                    (b.Groomer != null && b.Groomer.FullName.ToLower().Contains(term))
                );
            }

            var normalizedStatus = string.IsNullOrWhiteSpace(statusFilter) ? "all" : statusFilter.Trim().ToLowerInvariant();
            query = normalizedStatus switch
            {
                "pending" => query.Where(b => b.SpaStatus == "0" || b.SpaStatus.EndsWith("|0")),
                "inprogress" => query.Where(b => b.SpaStatus != "Cancelled" && b.SpaStatus != "4" && !b.SpaStatus.EndsWith("|4") && b.SpaStatus != "0" && !b.SpaStatus.EndsWith("|0")),
                "completed" => query.Where(b => b.SpaStatus == "4" || b.SpaStatus.EndsWith("|4")),
                "cancelled" => query.Where(b => b.SpaStatus == "Cancelled"),
                _ => query
            };

            query = query.OrderByDescending(b => b.BookingId);

            var totalFilteredItems = await query.CountAsync();
            var pageSize = 5;
            var totalPages = totalFilteredItems == 0 ? 0 : (int)Math.Ceiling(totalFilteredItems / (double)pageSize);
            var currentPage = page < 1 ? 1 : page;

            if (totalPages > 0 && currentPage > totalPages)
            {
                currentPage = totalPages;
            }

            var visibleBookings = await query
                .Skip((currentPage - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // Batch sync payment status with POS orders to avoid N+1 queries
            var unpaidBookingsWithOrders = new List<(SpaBooking Booking, string OrderId)>();
            foreach (var booking in visibleBookings)
            {
                if (booking.Status != "Đã thanh toán" && booking.Status != "Success" && booking.Status != "PAID")
                {
                    if (!string.IsNullOrEmpty(booking.Notes))
                    {
                        var match = System.Text.RegularExpressions.Regex.Match(booking.Notes, @"\[POS\s+(OD-\d+)\]");
                        if (match.Success)
                        {
                            unpaidBookingsWithOrders.Add((booking, match.Groups[1].Value));
                        }
                    }
                }
            }

            if (unpaidBookingsWithOrders.Any())
            {
                var orderIds = unpaidBookingsWithOrders.Select(x => x.OrderId).Distinct().ToList();
                var ordersMap = await _context.Orders
                    .AsNoTracking()
                    .Where(o => orderIds.Contains(o.OrderId))
                    .ToDictionaryAsync(o => o.OrderId, o => o.Status);

                bool hasChanges = false;
                foreach (var item in unpaidBookingsWithOrders)
                {
                    if (ordersMap.TryGetValue(item.OrderId, out var orderStatus))
                    {
                        if (orderStatus == "Đã thanh toán" || orderStatus == "Chờ xử lý" || orderStatus == "PAID")
                        {
                            item.Booking.Status = "Đã thanh toán";
                            _context.Entry(item.Booking).State = EntityState.Modified;
                            hasChanges = true;
                        }
                    }
                }

                if (hasChanges)
                {
                    await _context.SaveChangesAsync();
                }
            }

            var visibleBookingIds = visibleBookings.Select(b => b.BookingId).ToList();
            var reviewedBookingIds = await _context.SpaReviews
                .AsNoTracking()
                .Where(r => visibleBookingIds.Contains(r.BookingId))
                .Select(r => r.BookingId)
                .ToListAsync();

            ViewBag.ReviewedBookingIds = reviewedBookingIds;

            var hasAnyBookings = await _context.SpaBookings.AsNoTracking().AnyAsync(b => b.CustomerId == layout.Customer.CustomerId);

            var model = new SpaBookingHistoryPageViewModel
            {
                User = layout.User,
                Customer = layout.Customer,
                ActiveNav = "spabooking",
                Bookings = hasAnyBookings ? new List<SpaBooking> { new SpaBooking() } : new List<SpaBooking>(),
                VisibleBookings = visibleBookings,
                SearchTerm = normalizedSearch,
                StatusFilter = normalizedStatus,
                Page = totalPages == 0 ? 1 : currentPage,
                PageSize = pageSize,
                TotalFilteredItems = totalFilteredItems,
                TotalPages = totalPages
            };

            return View(model);
        }

        private async Task<ManagePetStore.Areas.Customer.Models.CustomerSidebarViewModel?> BuildSidebarViewModelAsync(string activeNav)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out var userId))
            {
                return null;
            }

            var user = await _context.Users
                .Include(u => u.Role)
                .Include(u => u.Customer)
                .FirstOrDefaultAsync(u => u.UserId == userId);

            if (user?.Customer == null)
            {
                return null;
            }

            return new ManagePetStore.Areas.Customer.Models.CustomerSidebarViewModel
            {
                User = user,
                Customer = user.Customer,
                ActiveNav = activeNav
            };
        }

        [HttpGet("GetProgress")]
        public async Task<IActionResult> GetProgress(int bookingId)
        {
            var customer = await GetCurrentCustomerAsync();
            if (customer == null)
            {
                return Json(new { success = false, message = "Bạn phải đăng nhập." });
            }

            var booking = await _context.SpaBookings
                .Include(b => b.Service)
                .FirstOrDefaultAsync(b => b.BookingId == bookingId && b.CustomerId == customer.CustomerId);

            if (booking == null)
            {
                return Json(new { success = false, message = "Không tìm thấy thông tin lịch hẹn." });
            }

            // Sync payment status with POS order if unpaid
            if (booking.Status != "Đã thanh toán" && booking.Status != "Success" && booking.Status != "PAID" && !string.IsNullOrEmpty(booking.Notes))
            {
                var match = System.Text.RegularExpressions.Regex.Match(booking.Notes, @"\[POS\s+(OD-\d+)\]");
                if (match.Success)
                {
                    string orderId = match.Groups[1].Value;
                    var orderStatus = await _context.Orders
                        .AsNoTracking()
                        .Where(o => o.OrderId == orderId)
                        .Select(o => o.Status)
                        .FirstOrDefaultAsync();

                    if (orderStatus == "Đã thanh toán" || orderStatus == "Chờ xử lý" || orderStatus == "PAID")
                    {
                        booking.Status = "Đã thanh toán";
                        await _context.SaveChangesAsync();
                    }
                }
            }

            var statuses = new[] { "Tiếp nhận", "Tắm & Sấy", "Cắt & Tỉa", "Massage", "Hoàn thành" };
            var completedIndexes = new List<int>();
            int activeIndex = 0;

            var dbStatus = booking.SpaStatus ?? "0";

            if (dbStatus == "Cancelled")
            {
                return Json(new { success = true, isCancelled = true, isCompleted = false,
                    serviceName = booking.Service?.Name ?? "Dịch vụ Spa",
                    bookingDate = booking.DateTime.ToString("dd/MM/yyyy HH:mm"),
                    activeIndex = -1, completedIndexes, notes = booking.Notes ?? "" });
            }

            if (dbStatus.Contains("|"))
            {
                // Format mới: "0,1,2|3" hoặc "0,1,2,3,4|4"
                var parts = dbStatus.Split('|');
                if (!string.IsNullOrEmpty(parts[0]))
                {
                    completedIndexes = parts[0].Split(',')
                        .Where(s => int.TryParse(s.Trim(), out _))
                        .Select(s => int.Parse(s.Trim()))
                        .ToList();
                }
                int.TryParse(parts[1], out activeIndex);
            }
            else if (int.TryParse(dbStatus, out int numericIdx))
            {
                // Format số thuần: "0", "1", "2"...
                // Các bước trước đó coi là completed
                for (int i = 0; i < numericIdx; i++) completedIndexes.Add(i);
                activeIndex = numericIdx;
            }
            else
            {
                // Format text cũ: "Tiếp nhận", "Running", "Hoàn thành"
                int idx = Array.IndexOf(statuses, dbStatus);
                if (idx == -1 && (dbStatus == "Running" || dbStatus == "InProgress")) idx = 1;
                if (idx == -1) idx = 0;
                // Các bước trước đó coi là completed
                for (int i = 0; i < idx; i++) completedIndexes.Add(i);
                activeIndex = idx;
            }

            // Chỉ coi là hoàn thành toàn bộ 5 bước kỹ thuật nếu activeIndex == 4 hoặc SpaStatus kết thúc bằng "|4" / "4"
            bool isFullyCompleted = (activeIndex == 4 && completedIndexes.Contains(4))
                || dbStatus == "4"
                || dbStatus.EndsWith("|4")
                || dbStatus == "Hoàn thành";
            int resolvedActiveIndex = isFullyCompleted ? -1 : activeIndex;

            // Nếu hoàn thành toàn bộ, đảm bảo tất cả 5 bước đều trong completedIndexes
            if (isFullyCompleted)
            {
                completedIndexes = new List<int> { 0, 1, 2, 3, 4 };
            }

            return Json(new
            {
                success = true,
                isCancelled = false,
                isCompleted = isFullyCompleted,
                serviceName = booking.Service?.Name ?? "Dịch vụ Spa",
                bookingDate = booking.DateTime.ToString("dd/MM/yyyy HH:mm"),
                activeIndex = resolvedActiveIndex,
                completedIndexes = completedIndexes,
                notes = booking.Notes ?? "Không có dặn dò đặc biệt."
            });
        }

        [HttpPost("Cancel")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(int bookingId, string reason)
        {
            var customer = await GetCurrentCustomerAsync();
            if (customer == null)
            {
                return Json(new { success = false, message = "Bạn cần đăng nhập trước." });
            }

            var booking = await _context.SpaBookings
                .Include(b => b.Pet)
                .FirstOrDefaultAsync(b => b.BookingId == bookingId && b.CustomerId == customer.CustomerId);

            if (booking == null)
            {
                return Json(new { success = false, message = "Không tìm thấy lịch hẹn." });
            }

            if (booking.SpaStatus == "Cancelled")
            {
                return Json(new { success = false, message = "Lịch hẹn này đã được hủy trước đó." });
            }

            // Ràng buộc 1: Chưa bấm bắt đầu (chỉ được hủy khi ca còn ở trạng thái Đang chờ/Tiếp nhận)
            bool isPending = booking.SpaStatus == "0" || (booking.SpaStatus != null && booking.SpaStatus.EndsWith("|0")) || booking.SpaStatus == "Tiếp nhận";
            if (!isPending)
            {
                return Json(new { success = false, message = "Lịch hẹn đã được nhân viên tiếp nhận bắt đầu thực hiện, không thể hủy." });
            }

            // Ràng buộc 2: chỉ được hủy trước tối thiểu 2 giờ
            if (booking.DateTime <= DateTime.Now.AddHours(2))
            {
                return Json(new { success = false, message = "Không thể hủy lịch hẹn đã cận giờ thực hiện (cần hủy trước tối thiểu 2 tiếng)." });
            }

            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    booking.SpaStatus = "Cancelled";
                    booking.Notes = $"[Khách tự hủy: {reason?.Trim()}] " + (booking.Notes ?? "");
                    _context.SpaBookings.Update(booking);

                    // Giải phóng hàng đợi SpaQueues nếu có
                    var queueItem = await _context.SpaQueues
                        .FirstOrDefaultAsync(q => q.PetName == booking.Pet.Name && q.ArrivalTime == booking.DateTime);
                    if (queueItem != null)
                    {
                        _context.SpaQueues.Remove(queueItem);
                    }

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    return Json(new { success = true, message = "Đã hủy lịch hẹn thành công!" });
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    return Json(new { success = false, message = $"Lỗi hệ thống khi hủy: {ex.Message}" });
                }
            }
        }

        [HttpPost("SubmitReview")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitReview(int bookingId, int ratingStar, string? comment)
        {
            var customer = await GetCurrentCustomerAsync();
            if (customer == null)
            {
                return Json(new { success = false, message = "Bạn phải đăng nhập." });
            }

            var booking = await _context.SpaBookings
                .FirstOrDefaultAsync(b => b.BookingId == bookingId && b.CustomerId == customer.CustomerId);

            if (booking == null)
            {
                return Json(new { success = false, message = "Không tìm thấy lịch hẹn hợp lệ." });
            }

            bool isPaid = booking.Status == "Đã thanh toán" || booking.Status == "Success" || booking.Status == "PAID";
            bool isTechnicallyDone = booking.SpaStatus == "4" || (booking.SpaStatus != null && booking.SpaStatus.EndsWith("|4"));

            if (!isTechnicallyDone || !isPaid)
            {
                return Json(new { success = false, message = "Chỉ có thể đánh giá dịch vụ sau khi ca làm việc đã hoàn thành và đã thanh toán tiền." });
            }

            if (ratingStar < 1 || ratingStar > 5)
            {
                return Json(new { success = false, message = "Số sao đánh giá phải từ 1 đến 5." });
            }

            // Kiểm tra xem đã đánh giá chưa
            var existingReview = await _context.SpaReviews.FirstOrDefaultAsync(r => r.BookingId == bookingId);
            if (existingReview != null)
            {
                return Json(new { success = false, message = "Bạn đã đánh giá lịch hẹn này rồi." });
            }

            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    var review = new SpaReview
                    {
                        BookingId = bookingId,
                        ServiceId = booking.ServiceId,
                        GroomerId = booking.GroomerId,
                        RatingStar = ratingStar,
                        Comment = comment?.Trim(),
                        CreatedAt = DateTime.Now
                    };
                    _context.SpaReviews.Add(review);
                    await _context.SaveChangesAsync();

                    // Cập nhật RatingsAverage của Groomer (BR-26)
                    var groomerProfile = await _context.StaffProfiles.FirstOrDefaultAsync(p => p.UserId == booking.GroomerId);
                    if (groomerProfile != null)
                    {
                        // Lấy tất cả đánh giá của Groomer này
                        var allGroomerReviews = await _context.SpaReviews
                            .Where(r => r.GroomerId == booking.GroomerId)
                            .Select(r => r.RatingStar)
                            .ToListAsync();

                        if (allGroomerReviews.Any())
                        {
                            decimal avg = (decimal)allGroomerReviews.Average();
                            groomerProfile.RatingsAverage = Math.Round(avg, 2);
                            _context.StaffProfiles.Update(groomerProfile);
                        }
                    }

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    return Json(new { success = true, message = "Cảm ơn bạn đã gửi phản hồi đánh giá dịch vụ!" });
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    return Json(new { success = false, message = $"Lỗi hệ thống khi gửi đánh giá: {ex.Message}" });
                }
            }
        }

        [HttpPost("PayCash")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PayCash(int bookingId)
        {
            var customer = await GetCurrentCustomerAsync();
            if (customer == null)
            {
                return Json(new { success = false, message = "Bạn cần đăng nhập trước." });
            }

            var booking = await _context.SpaBookings
                .FirstOrDefaultAsync(b => b.BookingId == bookingId && b.CustomerId == customer.CustomerId);

            if (booking == null)
            {
                return Json(new { success = false, message = "Không tìm thấy lịch hẹn." });
            }

            string orderId = "";
            if (!string.IsNullOrEmpty(booking.Notes))
            {
                var match = System.Text.RegularExpressions.Regex.Match(booking.Notes, @"\[POS\s+(OD-\d+)\]");
                if (match.Success)
                {
                    orderId = match.Groups[1].Value;
                }
            }

            if (string.IsNullOrEmpty(orderId))
            {
                return Json(new { success = false, message = "Lịch hẹn chưa được thu ngân lập hóa đơn thanh toán." });
            }

            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    booking.Status = "Đã thanh toán";
                    _context.SpaBookings.Update(booking);

                    var order = await _context.Orders.FindAsync(orderId);
                    if (order != null)
                    {
                        order.Status = "Đã thanh toán";
                        order.OrderStatus = 2;
                        _context.Orders.Update(order);
                    }

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    return Json(new { success = true, message = "Đã xác nhận thanh toán tiền mặt thành công!" });
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    return Json(new { success = false, message = $"Lỗi hệ thống khi thanh toán: {ex.Message}" });
                }
            }
        }

        [HttpGet("CheckBookingPaymentStatus")]
        public async Task<IActionResult> CheckBookingPaymentStatus(int bookingId)
        {
            var customer = await GetCurrentCustomerAsync();
            if (customer == null)
            {
                return Json(new { success = false, message = "Bạn cần đăng nhập trước." });
            }

            var booking = await _context.SpaBookings
                .FirstOrDefaultAsync(b => b.BookingId == bookingId && b.CustomerId == customer.CustomerId);

            if (booking == null)
            {
                return Json(new { success = false, message = "Không tìm thấy lịch hẹn." });
            }

            if (booking.Status == "Đã thanh toán" || booking.Status == "Success" || booking.Status == "PAID")
            {
                return Json(new { success = true, paid = true });
            }

            string orderId = "";
            if (!string.IsNullOrEmpty(booking.Notes))
            {
                var match = System.Text.RegularExpressions.Regex.Match(booking.Notes, @"\[POS\s+(OD-\d+)\]");
                if (match.Success)
                {
                    orderId = match.Groups[1].Value;
                }
            }

            if (!string.IsNullOrEmpty(orderId))
            {
                var order = await _context.Orders.FindAsync(orderId);
                if (order != null && (order.Status == "Đã thanh toán" || order.Status == "Chờ xử lý" || order.Status == "PAID"))
                {
                    booking.Status = "Đã thanh toán";
                    _context.SpaBookings.Update(booking);
                    await _context.SaveChangesAsync();

                    return Json(new { success = true, paid = true });
                }

                if (order != null && order.Status == "Chờ thanh toán")
                {
                    var parts = orderId.Split('-');
                    if (parts.Length >= 2 && long.TryParse(parts[^1], out long orderCode))
                    {
                        try
                        {
                            var paymentInfo = await _payOS.PaymentRequests.GetAsync(orderCode);
                            if (paymentInfo != null && paymentInfo.Status.ToString().ToUpper() == "PAID")
                            {
                                order.Status = "Đã thanh toán";
                                order.OrderStatus = 2;
                                _context.Orders.Update(order);

                                booking.Status = "Đã thanh toán";
                                _context.SpaBookings.Update(booking);

                                await _context.SaveChangesAsync();
                                return Json(new { success = true, paid = true });
                            }
                        }
                        catch (Exception)
                        {
                        }
                    }
                }
            }

            return Json(new { success = true, paid = false });
        }
    }
    public class SpaBookingHistoryPageViewModel : ManagePetStore.Areas.Customer.Models.CustomerSidebarViewModel
    {
        public List<SpaBooking> Bookings { get; set; } = [];
        public List<SpaBooking> VisibleBookings { get; set; } = [];
        public string? SearchTerm { get; set; }
        public string StatusFilter { get; set; } = "all";
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 5;
        public int TotalFilteredItems { get; set; }
        public int TotalPages { get; set; }
    }
}
