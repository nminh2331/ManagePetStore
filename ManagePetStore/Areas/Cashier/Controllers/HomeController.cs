using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ManagePetStore.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ManagePetStore.Areas.Cashier.Controllers
{
    [Area("Cashier")]
    [Authorize(Roles = "cashier,admin,manager")]
    public class HomeController : Controller
    {
        private readonly PetStoreManagementContext _context;

        public HomeController(PetStoreManagementContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            // Tải danh sách đơn hàng Spa nháp đang chờ thanh toán
            var pendingOrders = await _context.Orders
                .Include(o => o.Customer)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.SpaService)
                .Where(o => o.Status == "Chờ thanh toán" && o.OrderId.StartsWith("ORD-SPA-"))
                .OrderByDescending(o => o.Date)
                .ToListAsync();

            return View(pendingOrders);
        }

        [HttpGet]
        public async Task<IActionResult> CheckVoucher(string code, decimal orderAmount)
        {
            if (string.IsNullOrEmpty(code))
            {
                return Json(new { success = false, message = "Vui lòng nhập mã Voucher." });
            }

            var voucher = await _context.Vouchers.FirstOrDefaultAsync(v => v.Code.ToUpper() == code.Trim().ToUpper());
            if (voucher == null)
            {
                return Json(new { success = false, message = "Mã Voucher không tồn tại." });
            }

            if (!voucher.Status)
            {
                return Json(new { success = false, message = "Voucher này đã bị vô hiệu hóa." });
            }

            if (voucher.ExpiryDate < DateTime.Now)
            {
                return Json(new { success = false, message = "Voucher này đã hết hạn sử dụng." });
            }

            if (orderAmount < voucher.MinOrder)
            {
                return Json(new { success = false, message = $"Giá trị đơn hàng chưa đạt mức tối thiểu ({voucher.MinOrder.ToString("N0")}đ) để áp dụng." });
            }

            decimal discount = 0;
            if (voucher.Type.ToLower() == "percentage")
            {
                discount = orderAmount * (voucher.Value / 100m);
            }
            else // fixed
            {
                discount = voucher.Value;
            }

            if (discount > orderAmount)
            {
                discount = orderAmount;
            }

            return Json(new { success = true, discount = discount, newTotal = orderAmount - discount });
        }

        [HttpPost]
        public async Task<IActionResult> ConfirmPayment(string orderId, string paymentMethod, string? voucherCode)
        {
            if (string.IsNullOrEmpty(orderId))
            {
                return Json(new { success = false, message = "Mã hóa đơn không hợp lệ." });
            }

            var order = await _context.Orders
                .Include(o => o.OrderItems)
                .FirstOrDefaultAsync(o => o.OrderId == orderId);

            if (order == null)
            {
                return Json(new { success = false, message = "Không tìm thấy hóa đơn." });
            }

            if (order.Status == "Success")
            {
                return Json(new { success = false, message = "Hóa đơn này đã được thanh toán trước đó." });
            }

            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    // Tính toán lại Voucher trên Server để đảm bảo tính an toàn bảo mật
                    decimal discount = 0;
                    if (!string.IsNullOrEmpty(voucherCode))
                    {
                        var voucher = await _context.Vouchers
                            .FirstOrDefaultAsync(v => v.Code.ToUpper() == voucherCode.Trim().ToUpper());
                        
                        if (voucher != null && voucher.Status && voucher.ExpiryDate >= DateTime.Now && order.Subtotal >= voucher.MinOrder)
                        {
                            if (voucher.Type.ToLower() == "percentage")
                            {
                                discount = order.Subtotal * (voucher.Value / 100m);
                            }
                            else
                            {
                                discount = voucher.Value;
                            }

                            if (discount > order.Subtotal)
                            {
                                discount = order.Subtotal;
                            }
                        }
                    }

                    // 1. Cập nhật thông tin hóa đơn (Đã loại bỏ hoàn toàn phần LoyaltyPoints cộng điểm)
                    order.Status = "Success";
                    order.PaymentMethod = string.IsNullOrEmpty(paymentMethod) ? "Tiền mặt" : paymentMethod;
                    order.Discount = discount;
                    order.Total = order.Subtotal - discount;
                    _context.Orders.Update(order);

                    // 2. Tìm và cập nhật SpaBooking tương ứng
                    if (orderId.StartsWith("ORD-SPA-"))
                    {
                        var parts = orderId.Split('-');
                        if (parts.Length >= 4 && int.TryParse(parts[2], out var bookingId))
                        {
                            var booking = await _context.SpaBookings.FindAsync(bookingId);
                            if (booking != null)
                            {
                                booking.Status = "Đã thanh toán";
                                _context.SpaBookings.Update(booking);
                            }
                        }
                    }
                    else
                    {
                        var spaItem = order.OrderItems.FirstOrDefault(oi => oi.SpaServiceId.HasValue);
                        if (spaItem != null)
                        {
                            var booking = await _context.SpaBookings
                                .FirstOrDefaultAsync(b => b.CustomerId == order.CustomerId 
                                                       && b.ServiceId == spaItem.SpaServiceId.Value 
                                                       && b.Status == "Chưa thanh toán");
                            if (booking != null)
                            {
                                booking.Status = "Đã thanh toán";
                                _context.SpaBookings.Update(booking);
                            }
                        }
                    }

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    return Json(new { success = true, message = "Xác nhận thanh toán hóa đơn thành công!" });
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    return Json(new { success = false, message = $"Lỗi hệ thống: {ex.Message}" });
                }
            }
        }
    }
}
