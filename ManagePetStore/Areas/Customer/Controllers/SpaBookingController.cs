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

using Microsoft.AspNetCore.SignalR;
using ManagePetStore.Hubs;
using ManagePetStore.Areas.ServiceStaff.Helpers;

namespace ManagePetStore.Areas.Customer.Controllers
{
    /// <summary>
    /// =========================================================================================
    /// NGƯỜI THỰC HIỆN / TÁC GIẢ: NHẬT MINH
    /// CHỨC NĂNG: Controller quản lý Đặt lịch Spa, Xem Lịch sử Spa, Theo dõi Tiến độ Spa Real-time, 
    ///            Đánh giá Dịch vụ Spa và Thanh toán Hóa đơn Spa phía Khách hàng (Customer).
    /// CÁC USE CASE CỦA NHẬT MINH TRONG CONTROLLER NÀY:
    /// - UC-21: View Spa Services List (Xem danh sách gói dịch vụ Spa)
    /// - UC-22: Book Spa Appointment (Đặt lịch hẹn Spa theo thời gian / thú cưng)
    /// - UC-23: Service Rating & Review (Đánh giá chất lượng dịch vụ Spa & số sao)
    /// - UC-25: Spa Invoice Payment (Thanh toán hóa đơn Spa qua tiền mặt / chuyển khoản QR PayOS)
    /// - UC-26: View Spa Appointment History (Xem danh sách lịch sử đặt ca Spa)
    /// - UC-27: Spa Progress Tracking (Theo dõi tiến độ chăm sóc thú cưng 5 bước thời gian thực)
    /// - UC-47: Cancel Booking as Customer (Khách hàng tự hủy lịch hẹn Spa trước giờ thực hiện)
    /// =========================================================================================
    /// </summary>
    [Area("Customer")]
    [Authorize]
    [Route("Customer/SpaBooking")]
    public class SpaBookingController : Controller
    {
        private readonly PetStoreManagementContext _context;
        private readonly PayOSClient _payOS;
        private readonly IHubContext<ReviewHub> _reviewHubContext;

        /// <summary>
        /// NGƯỜI THỰC HIỆN: Nhật Minh
        /// CHỨC NĂNG: Constructor khởi tạo các Dependency Injection chính cho Controller SpaBooking (DbContext, PayOS, SignalR Hub).
        /// </summary>
        public SpaBookingController(PetStoreManagementContext context, PayOSClient payOS, IHubContext<ReviewHub> reviewHubContext)
        {
            _context = context;
            _payOS = payOS;
            _reviewHubContext = reviewHubContext;
        }

        /// <summary>
        /// NGƯỜI THỰC HIỆN: Nhật Minh
        /// CHỨC NĂNG: Hàm hỗ trợ lấy thông tin Khách hàng (Customer) hiện tại dựa trên User Claims đăng nhập.
        /// </summary>
        /// <returns>Trả về đối tượng Customer hoặc null nếu chưa đăng nhập / không tìm thấy</returns>
        private async Task<ManagePetStore.Models.Customer?> GetCurrentCustomerAsync()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out var userId))
            {
                return null;
            }

            return await _context.Customers.FirstOrDefaultAsync(c => c.UserId == userId);
        }

        /// <summary>
        /// NGƯỜI THỰC HIỆN: Nhật Minh
        /// CHỨC NĂNG: UC-26 (View Spa Appointment History) & UC-21 (View Spa Services List).
        /// Hiển thị danh sách Lịch sử Đặt ca Spa của khách hàng đăng nhập, hỗ trợ Tìm kiếm theo từ khóa, Lọc trạng thái (Chờ, Đang làm, Hoàn thành, Đã hủy) và Phân trang.
        /// Tự động đồng bộ trạng thái thanh toán với hóa đơn POS nếu khách đã thanh toán tại quầy.
        /// </summary>
        /// <param name="searchTerm">Từ khóa tìm kiếm (Mã lịch hẹn, tên dịch vụ, tên thú cưng, tên Groomer)</param>
        /// <param name="statusFilter">Bộ lọc trạng thái (all, pending, inprogress, completed, cancelled)</param>
        /// <param name="page">Số trang hiện tại (Mặc định 1, kích thước 5 item/trang)</param>
        [HttpGet("History")]
        public async Task<IActionResult> History(string? searchTerm, string statusFilter = "all", int page = 1)
        {
            var layout = await BuildSidebarViewModelAsync("spabooking");
            if (layout == null)
            {
                return RedirectToAction("Login", "Account", new { area = "Customer" });
            }

            // Nhật Minh: Truy vấn danh sách lịch hẹn Spa của riêng khách hàng hiện tại
            var query = _context.SpaBookings
                .AsNoTracking()
                .Include(b => b.Pet)
                .Include(b => b.Service)
                .Include(b => b.Groomer)
                .Where(b => b.CustomerId == layout.Customer.CustomerId);

            // Nhật Minh Validate & Filter: Lọc theo từ khóa tìm kiếm
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

            // Nhật Minh Validate & Filter: Lọc theo trạng thái 5 bước Spa do Nhật Minh thiết kế
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

            // Nhật Minh: Phân trang danh sách (PageSize = 5)
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

            // Nhật Minh: Đồng bộ trạng thái thanh toán từ hóa đơn POS sang lịch hẹn Spa để tránh tình trạng N+1 query
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

            // Nhật Minh: Lấy danh sách các BookingId đã được đánh giá để hiển thị nút/trạng thái Đánh giá
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

        /// <summary>
        /// NGƯỜI THỰC HIỆN: Nhật Minh
        /// CHỨC NĂNG: Hàm hỗ trợ xây dựng ViewModel cho Sidebar của trang Khách hàng.
        /// </summary>
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

        /// <summary>
        /// NGƯỜI THỰC HIỆN: Nhật Minh
        /// CHỨC NĂNG: UC-27 (Spa Progress Tracking - Real-time progress bar).
        /// API trả về tiến độ chăm sóc thú cưng 5 bước thực hiện (Tiếp nhận -> Tắm & Sấy -> Cắt & Tỉa -> Massage -> Hoàn thành) 
        /// theo thời gian thực để hiển thị trên thanh tiến độ phía Khách hàng.
        /// </summary>
        /// <param name="bookingId">Mã ID lịch hẹn Spa cần theo dõi tiến độ</param>
        [HttpGet("GetProgress")]
        public async Task<IActionResult> GetProgress(int bookingId)
        {
            // Nhật Minh Validate: Kiểm tra khách hàng đã đăng nhập chưa
            var customer = await GetCurrentCustomerAsync();
            if (customer == null)
            {
                return Json(new { success = false, message = "Bạn phải đăng nhập." });
            }

            // Nhật Minh Validate: Tìm lịch hẹn Spa thuộc về đúng khách hàng này
            var booking = await _context.SpaBookings
                .Include(b => b.Service)
                .FirstOrDefaultAsync(b => b.BookingId == bookingId && b.CustomerId == customer.CustomerId);

            if (booking == null)
            {
                return Json(new { success = false, message = "Không tìm thấy thông tin lịch hẹn." });
            }

            // Đồng bộ trạng thái thanh toán từ hóa đơn POS
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

            // Nhật Minh: 5 bước tiến độ chuẩn của Dịch vụ Spa
            var statuses = new[] { "Tiếp nhận", "Tắm & Sấy", "Cắt & Tỉa", "Massage", "Hoàn thành" };
            var completedIndexes = new List<int>();
            int activeIndex = 0;

            var dbStatus = booking.SpaStatus ?? "0";

            // Nếu ca Spa đã bị hủy
            if (dbStatus == "Cancelled")
            {
                return Json(new { success = true, isCancelled = true, isCompleted = false,
                    serviceName = booking.Service?.Name ?? "Dịch vụ Spa",
                    bookingDate = booking.DateTime.ToString("dd/MM/yyyy HH:mm"),
                    activeIndex = -1, completedIndexes, notes = booking.Notes ?? "" });
            }

            // Nhật Minh: Phân giải chuỗi nén SpaStatus (ví dụ: "0,1,2|3" nghĩa là completed bước 0,1,2 và active bước 3)
            if (dbStatus.Contains("|"))
            {
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
                for (int i = 0; i < numericIdx; i++) completedIndexes.Add(i);
                activeIndex = numericIdx;
            }
            else
            {
                int idx = Array.IndexOf(statuses, dbStatus);
                if (idx == -1 && (dbStatus == "Running" || dbStatus == "InProgress")) idx = 1;
                if (idx == -1) idx = 0;
                for (int i = 0; i < idx; i++) completedIndexes.Add(i);
                activeIndex = idx;
            }

            // Nhật Minh: Kiểm tra xem đã hoàn thành toàn bộ 5 bước kỹ thuật chưa
            bool isFullyCompleted = (activeIndex == 4 && completedIndexes.Contains(4))
                || dbStatus == "4"
                || dbStatus.EndsWith("|4")
                || dbStatus == "Hoàn thành";
            int resolvedActiveIndex = isFullyCompleted ? -1 : activeIndex;

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

        /// <summary>
        /// NGƯỜI THỰC HIỆN: Nhật Minh
        /// CHỨC NĂNG: UC-47 (Cancel Booking as Customer).
        /// Khách hàng tự hủy lịch hẹn Spa trước giờ thực hiện.
        /// PHẦN VALIDATE DỮ LIỆU CỦA NHẬT MINH:
        /// 1. Kiểm tra đăng nhập tài khoản khách hàng.
        /// 2. Sử dụng SpaServiceValidationHelper.ValidateSpaCancellation để validate:
        ///    - Lý do hủy không được rỗng.
        ///    - Ca Spa chưa ở trạng thái "Cancelled".
        ///    - Ca Spa phải đang ở bước tiếp nhận ("0", "|0", "Tiếp nhận"). Nếu đã tắm sấy/cắt tỉa không cho hủy.
        ///    - Thời gian hủy phải trước mốc hẹn ít nhất 2 giờ.
        /// 3. Hủy trong TransactionDB, giải phóng SpaQueues và gửi thông báo SignalR thời gian thực cho Staff.
        /// </summary>
        /// <param name="bookingId">Mã ID lịch hẹn Spa cần hủy</param>
        /// <param name="reason">Lý do hủy lịch hẹn</param>
        [HttpPost("Cancel")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(int bookingId, string reason)
        {
            // Nhật Minh Validate 1: Đảm bảo khách đã đăng nhập
            var customer = await GetCurrentCustomerAsync();
            if (customer == null)
            {
                return Json(new { success = false, message = "Bạn cần đăng nhập trước." });
            }

            // Nhật Minh Validate 2: Đảm bảo lịch hẹn tồn tại và thuộc về đúng khách hàng này
            var booking = await _context.SpaBookings
                .Include(b => b.Pet)
                .Include(b => b.Service)
                .FirstOrDefaultAsync(b => b.BookingId == bookingId && b.CustomerId == customer.CustomerId);

            if (booking == null)
            {
                return Json(new { success = false, message = "Không tìm thấy lịch hẹn." });
            }

            // Nhật Minh Validate 3: Gọi SpaServiceValidationHelper.ValidateSpaCancellation để kiểm tra lý do, trạng thái và thời gian cận giờ
            var (isCancelValid, cancelErrorMsg) = SpaServiceValidationHelper.ValidateSpaCancellation(booking.DateTime, booking.SpaStatus ?? "0", reason);
            if (!isCancelValid)
            {
                return Json(new { success = false, message = cancelErrorMsg });
            }

            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    // Nhật Minh: Đổi trạng thái sang Cancelled và ghi nhận lý do hủy vào ghi chú
                    booking.SpaStatus = "Cancelled";
                    booking.Notes = $"[Khách tự hủy: {reason?.Trim()}] " + (booking.Notes ?? "");
                    _context.SpaBookings.Update(booking);

                    // Nhật Minh: Giải phóng hàng đợi SpaQueues nếu có
                    var queueItem = await _context.SpaQueues
                        .FirstOrDefaultAsync(q => q.PetName == booking.Pet.Name && q.ArrivalTime == booking.DateTime);
                    if (queueItem != null)
                    {
                        _context.SpaQueues.Remove(queueItem);
                    }

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    // Nhật Minh: Phát thông báo SignalR thời gian thực đến giao diện của Staff
                    try
                    {
                        var notificationData = new
                        {
                            bookingId = booking.BookingId,
                            customerName = customer.FullName,
                            petName = booking.Pet?.Name ?? "Thú cưng",
                            serviceName = booking.Service?.Name ?? "Dịch vụ Spa",
                            bookingDate = booking.DateTime.ToString("HH:mm dd/MM/yyyy"),
                            reason = reason?.Trim()
                        };

                        await _reviewHubContext.Clients.All.SendAsync("ReceiveSpaCancellationNotification", notificationData);
                        await _reviewHubContext.Clients.Group("StaffGroup").SendAsync("ReceiveSpaCancellationNotification", notificationData);
                    }
                    catch
                    {
                        // Lỗi gửi SignalR không làm hỏng giao dịch hủy DB
                    }

                    return Json(new { success = true, message = "Đã hủy lịch hẹn thành công!" });
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    return Json(new { success = false, message = $"Lỗi hệ thống khi hủy: {ex.Message}" });
                }
            }
        }

        /// <summary>
        /// NGƯỜI THỰC HIỆN: Nhật Minh
        /// CHỨC NĂNG: UC-23 (Service Rating & Review).
        /// Khách hàng viết bình luận và gửi số sao đánh giá chất lượng dịch vụ Spa kèm ảnh chụp thực tế.
        /// PHẦN VALIDATE DỮ LIỆU CỦA NHẬT MINH:
        /// 1. Kiểm tra đăng nhập tài khoản.
        /// 2. RÀNG BUỘC KINH DOANH: Chỉ được phép đánh giá sau khi ca Spa ĐÃ HOÀN THÀNH (bước 5 / index 4) và ĐÃ THANH TOÁN TIỀN.
        /// 3. Sử dụng SpaServiceValidationHelper.ValidateSpaReview để kiểm tra:
        ///    - Số sao đánh giá từ 1 đến 5 sao.
        ///    - Tệp ảnh đính kèm (nếu có): chỉ nhận PNG, JPG, JPEG và dung lượng < 100MB (hoặc < 20MB theo giới hạn UI).
        /// 4. Kiểm tra không cho phép gửi 2 lần đánh giá cho cùng 1 lịch hẹn.
        /// 5. Tự động tính toán lại điểm đánh giá trung bình RatingsAverage của Groomer (BR-26).
        /// </summary>
        /// <param name="bookingId">Mã ID lịch hẹn Spa</param>
        /// <param name="ratingStar">Số sao rèn luyện/đánh giá (1-5 sao)</param>
        /// <param name="comment">Nội dung nhận xét nhận được từ khách hàng</param>
        /// <param name="reviewImage">Tệp hình ảnh minh họa trải nghiệm (nếu có)</param>
        [HttpPost("SubmitReview")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitReview(int bookingId, int ratingStar, string? comment, IFormFile? reviewImage)
        {
            // Nhật Minh Validate 1: Đảm bảo khách đã đăng nhập
            var customer = await GetCurrentCustomerAsync();
            if (customer == null)
            {
                return Json(new { success = false, message = "Bạn phải đăng nhập." });
            }

            // Nhật Minh Validate 2: Tìm đúng lịch hẹn Spa
            var booking = await _context.SpaBookings
                .FirstOrDefaultAsync(b => b.BookingId == bookingId && b.CustomerId == customer.CustomerId);

            if (booking == null)
            {
                return Json(new { success = false, message = "Không tìm thấy lịch hẹn hợp lệ." });
            }

            // Nhật Minh Validate 3 (Ràng buộc nghiệp vụ): Phải Hoàn Thành kỹ thuật và Đã Thanh Toán mới cho đánh giá
            bool isPaid = booking.Status == "Đã thanh toán" || booking.Status == "Success" || booking.Status == "PAID";
            bool isTechnicallyDone = booking.SpaStatus == "4" || (booking.SpaStatus != null && booking.SpaStatus.EndsWith("|4"));

            if (!isTechnicallyDone || !isPaid)
            {
                return Json(new { success = false, message = "Chỉ có thể đánh giá dịch vụ sau khi ca làm việc đã hoàn thành và đã thanh toán tiền." });
            }

            // Nhật Minh Validate 4: Kiểm tra số sao (1-5) và ảnh qua Helper
            var (isReviewValid, reviewErrorMsg) = SpaServiceValidationHelper.ValidateSpaReview(ratingStar, reviewImage);
            if (!isReviewValid)
            {
                return Json(new { success = false, message = reviewErrorMsg });
            }

            // Nhật Minh Validate 5: Kiểm tra xem lịch hẹn này đã được đánh giá chưa
            var existingReview = await _context.SpaReviews.FirstOrDefaultAsync(r => r.BookingId == bookingId);
            if (existingReview != null)
            {
                return Json(new { success = false, message = "Bạn đã đánh giá lịch hẹn này rồi." });
            }

            // Nhật Minh: Xử lý lưu tệp ảnh đính kèm vào thư mục /uploads/reviews/
            string? imageUrl = null;
            if (reviewImage != null && reviewImage.Length > 0)
            {
                var ext = System.IO.Path.GetExtension(reviewImage.FileName).ToLowerInvariant();
                var allowedExtensions = new[] { ".png", ".jpg", ".jpeg" };
                if (!allowedExtensions.Contains(ext))
                {
                    return Json(new { success = false, message = "Chỉ chấp nhận file ảnh đính kèm có định dạng PNG, JPG hoặc JPEG." });
                }

                if (reviewImage.Length >= 20 * 1024 * 1024)
                {
                    return Json(new { success = false, message = "Dung lượng ảnh đính kèm phải nhỏ hơn 20MB." });
                }

                var uploadsFolder = System.IO.Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "reviews");
                if (!System.IO.Directory.Exists(uploadsFolder))
                {
                    System.IO.Directory.CreateDirectory(uploadsFolder);
                }

                var uniqueFileName = $"{Guid.NewGuid()}{ext}";
                var filePath = System.IO.Path.Combine(uploadsFolder, uniqueFileName);
                using (var fileStream = new System.IO.FileStream(filePath, System.IO.FileMode.Create))
                {
                    await reviewImage.CopyToAsync(fileStream);
                }

                imageUrl = $"/uploads/reviews/{uniqueFileName}";
            }

            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    // Nhật Minh: Lưu bản ghi SpaReview mới
                    var review = new SpaReview
                    {
                        BookingId = bookingId,
                        ServiceId = booking.ServiceId,
                        GroomerId = booking.GroomerId,
                        RatingStar = ratingStar,
                        Comment = comment?.Trim(),
                        ImageUrl = imageUrl,
                        CreatedAt = DateTime.Now
                    };
                    _context.SpaReviews.Add(review);
                    await _context.SaveChangesAsync();

                    // Nhật Minh: Cập nhật lại RatingsAverage của Groomer theo nghiệp vụ BR-26
                    var groomerProfile = await _context.StaffProfiles.FirstOrDefaultAsync(p => p.UserId == booking.GroomerId);
                    if (groomerProfile != null)
                    {
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

                    // Nhật Minh: Đẩy đánh giá thời gian thực qua SignalR Hub
                    try
                    {
                        var sku = $"SPA-SVC-{booking.ServiceId:D3}";
                        await _reviewHubContext.Clients.Group(sku).SendAsync("ReceiveNewReview", new
                        {
                            customerName = customer.FullName,
                            rating = ratingStar,
                            comment = comment?.Trim(),
                            imageUrl = imageUrl,
                            createdAt = DateTime.Now.ToString("dd/MM/yyyy")
                        });
                    }
                    catch
                    {
                        // SignalR fail không ảnh hưởng DB
                    }

                    return Json(new { success = true, message = "Cảm ơn bạn đã gửi phản hồi đánh giá dịch vụ!" });
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    return Json(new { success = false, message = $"Lỗi hệ thống khi gửi đánh giá: {ex.Message}" });
                }
            }
        }

        /// <summary>
        /// NGƯỜI THỰC HIỆN: Nhật Minh
        /// CHỨC NĂNG: UC-25 (Spa Invoice Payment) & UC-31 (Process Spa Checkout & Payment).
        /// Xác nhận thanh toán tiền mặt cho lịch hẹn Spa đã được lập hóa đơn POS tại quầy.
        /// </summary>
        /// <param name="bookingId">Mã ID lịch hẹn Spa cần xác nhận thanh toán</param>
        [HttpPost("PayCash")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PayCash(int bookingId)
        {
            // Nhật Minh Validate 1: Đảm bảo khách đã đăng nhập
            var customer = await GetCurrentCustomerAsync();
            if (customer == null)
            {
                return Json(new { success = false, message = "Bạn cần đăng nhập trước." });
            }

            // Nhật Minh Validate 2: Kiểm tra lịch hẹn Spa tồn tại
            var booking = await _context.SpaBookings
                .FirstOrDefaultAsync(b => b.BookingId == bookingId && b.CustomerId == customer.CustomerId);

            if (booking == null)
            {
                return Json(new { success = false, message = "Không tìm thấy lịch hẹn." });
            }

            // Nhật Minh: Trích xuất Mã hóa đơn POS từ ghi chú (Notes)
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
                    // Nhật Minh: Cập nhật trạng thái "Đã thanh toán" cho SpaBooking và Order liên quan
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

        /// <summary>
        /// NGƯỜI THỰC HIỆN: Nhật Minh
        /// CHỨC NĂNG: UC-25 (Spa Invoice Payment).
        /// API Polling kiểm tra trạng thái thanh toán trực tuyến (PayOS QR code) của ca Spa.
        /// </summary>
        /// <param name="bookingId">Mã ID lịch hẹn Spa cần kiểm tra</param>
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

    /// <summary>
    /// NGƯỜI THỰC HIỆN: Nhật Minh
    /// CHỨC NĂNG: ViewModel quản lý trang Lịch sử Đặt lịch Spa (UC-26).
    /// </summary>
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
