using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using ManagePetStore.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ManagePetStore.Areas.Customer.Controllers
{
    [Area("Customer")]
    [Authorize]
    [Route("Customer/SpaBooking")]
    public class SpaBookingController : Controller
    {
        private readonly PetStoreManagementContext _context;

        public SpaBookingController(PetStoreManagementContext context)
        {
            _context = context;
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

            var bookings = await _context.SpaBookings
                .Include(b => b.Pet)
                .Include(b => b.Service)
                .Include(b => b.Groomer)
                .Where(b => b.CustomerId == layout.Customer.CustomerId)
                .OrderByDescending(b => b.DateTime)
                .ToListAsync();

            // Lọc kết quả tìm kiếm và trạng thái trong bộ nhớ
            var normalizedSearch = searchTerm?.Trim() ?? "";
            var normalizedStatus = string.IsNullOrWhiteSpace(statusFilter) ? "all" : statusFilter.Trim().ToLowerInvariant();

            IEnumerable<SpaBooking> filteredBookings = bookings;

            if (!string.IsNullOrWhiteSpace(normalizedSearch))
            {
                filteredBookings = filteredBookings.Where(b =>
                    b.BookingId.ToString().Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase) ||
                    (b.Service?.Name ?? "").Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase) ||
                    (b.Pet?.Name ?? "").Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase) ||
                    (b.Groomer?.FullName ?? "").Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase)
                );
            }

            filteredBookings = normalizedStatus switch
            {
                "pending" => filteredBookings.Where(b => b.SpaStatus == "0" || b.SpaStatus.EndsWith("|0")),
                "inprogress" => filteredBookings.Where(b => b.SpaStatus != "Cancelled" && b.SpaStatus != "4" && !b.SpaStatus.EndsWith("|4") && b.SpaStatus != "0" && !b.SpaStatus.EndsWith("|0")),
                "completed" => filteredBookings.Where(b => b.SpaStatus == "4" || b.SpaStatus.EndsWith("|4")),
                "cancelled" => filteredBookings.Where(b => b.SpaStatus == "Cancelled"),
                _ => filteredBookings
            };

            var filteredList = filteredBookings.ToList();
            var currentPage = page < 1 ? 1 : page;
            var totalFilteredItems = filteredList.Count;
            var pageSize = 5;
            var totalPages = totalFilteredItems == 0 ? 0 : (int)Math.Ceiling(totalFilteredItems / (double)pageSize);

            if (totalPages > 0 && currentPage > totalPages)
            {
                currentPage = totalPages;
            }

            var visibleBookings = filteredList
                .Skip((currentPage - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            // Lấy danh sách ID các booking đã được đánh giá
            var reviewedBookingIds = await _context.SpaReviews
                .Where(r => bookings.Select(b => b.BookingId).Contains(r.BookingId))
                .Select(r => r.BookingId)
                .ToListAsync();

            ViewBag.ReviewedBookingIds = reviewedBookingIds;

            var model = new SpaBookingHistoryPageViewModel
            {
                User = layout.User,
                Customer = layout.Customer,
                ActiveNav = "spabooking",
                Bookings = bookings,
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

            var statuses = new[] { "Tiếp nhận", "Tắm & Sấy", "Cắt & Tỉa", "Massage", "Hoàn thành" };
            var completedIndexes = new List<int>();
            int activeIndex = 0;

            var dbStatus = booking.SpaStatus ?? "0";
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

            return Json(new
            {
                success = true,
                isCancelled = booking.SpaStatus == "Cancelled",
                serviceName = booking.Service?.Name ?? "Dịch vụ Spa",
                bookingDate = booking.DateTime.ToString("dd/MM/yyyy HH:mm"),
                activeIndex = activeIndex,
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

            // Ràng buộc thời gian: chỉ được hủy trước tối thiểu 2 giờ
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

            if (booking.SpaStatus != "4" && !booking.SpaStatus.EndsWith("|4"))
            {
                return Json(new { success = false, message = "Chỉ có thể đánh giá dịch vụ sau khi ca làm việc đã hoàn thành." });
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
