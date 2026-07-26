using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ManagePetStore.Models;
using ManagePetStore.Areas.Cashier.Models;
using System;
using System.Linq;
using System.Threading.Tasks;
using PayOS;
using PayOS.Models;
using PayOS.Models.V2.PaymentRequests;
using ManagePetStore.Services.Warehouse;
using System.Security.Claims;
using System.Data;

namespace ManagePetStore.Areas.Cashier.Controllers
{
    [Area("Cashier")]
    [Authorize(Roles = "cashier,manager,admin")]
    public class OrderController : Controller
    {
        private readonly PetStoreManagementContext _context;
        private readonly PayOSClient _payOS;
        private readonly IStockMovementService _stockMovementService;
        private readonly IInventoryBatchService _inventoryBatchService;

        public OrderController(PetStoreManagementContext context, PayOSClient payOS, IStockMovementService stockMovementService, IInventoryBatchService inventoryBatchService)
        {
            _context = context;
            _payOS = payOS;
            _stockMovementService = stockMovementService;
            _inventoryBatchService = inventoryBatchService;
        }

        private async Task UpdateOrderToPaidAsync(Order order)
        {
            order.Status = "Chờ xử lý";
            order.OrderStatus = 2;
            _context.Entry(order).State = EntityState.Modified;

            // Sync linked SpaBookings to Đã thanh toán immediately
            var spaBookings = await _context.SpaBookings
                .Where(sb => sb.Notes != null && sb.Notes.Contains($"[POS {order.OrderId}]"))
                .ToListAsync();
            foreach (var sb in spaBookings)
            {
                sb.Status = "Đã thanh toán";
            }

            await _context.SaveChangesAsync();
        }

        private async Task CancelPendingPaymentOrderAsync(string orderId, string reason)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            var order = await _context.Orders
                .Include(item => item.Customer)
                .FirstOrDefaultAsync(item => item.OrderId == orderId);
            if (order == null)
            {
                return;
            }

            bool isPendingPayment = string.Equals(
                order.Status,
                "Chờ thanh toán",
                StringComparison.OrdinalIgnoreCase);
            bool isAlreadyCancelled = HotelCheckoutWorkflow.IsCancelled(order.Status);
            if (!isPendingPayment && !isAlreadyCancelled)
            {
                return;
            }

            if (isPendingPayment)
            {
                order.Status = "Đã hủy";
                order.OrderStatus = 0;
                order.CancelReason = reason;
                order.CanceledAt = DateTime.Now;
                order.CanceledBy = User.FindFirstValue(ClaimTypes.NameIdentifier)
                    ?? User.Identity?.Name
                    ?? "Cashier";
            }

            var hotelStatements = await _context.HotelCheckoutStatements
                .Where(statement => statement.OrderId == orderId)
                .ToListAsync();
            foreach (var statement in hotelStatements)
            {
                statement.OrderId = null;
                statement.Order = null;
                statement.Status = "ReadyForPayment";
                statement.PaidAt = null;
            }

            var spaBookings = await _context.SpaBookings
                .Include(booking => booking.Service)
                .Where(booking => booking.Notes != null &&
                                  booking.Notes.Contains($"[POS {orderId}]"))
                .ToListAsync();
            foreach (var booking in spaBookings)
            {
                booking.Status = "Chờ thanh toán";
                booking.Notes = RemoveCancelledOrderPrefix(
                    booking.Notes,
                    orderId,
                    booking.Service?.Name);
            }

            await _context.SaveChangesAsync();
            await ManagePetStore.Services.Customer.CustomerRewardHelper
                .RecalculateCustomerPointsAndTierAsync(order.CustomerId, _context);
            await transaction.CommitAsync();
        }

        private static string? RemoveCancelledOrderPrefix(
            string? notes,
            string orderId,
            string? serviceName)
        {
            if (string.IsNullOrWhiteSpace(notes))
            {
                return null;
            }

            string exactPrefix = $"[POS {orderId}] | Dịch vụ: {serviceName ?? string.Empty} ";
            if (notes.StartsWith(exactPrefix, StringComparison.OrdinalIgnoreCase))
            {
                string restored = notes[exactPrefix.Length..].Trim();
                return restored.Length == 0 ? null : restored;
            }

            string marker = $"[POS {orderId}]";
            int markerIndex = notes.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (markerIndex < 0)
            {
                return notes;
            }

            string restoredFallback = string.Concat(
                    notes.AsSpan(0, markerIndex),
                    notes.AsSpan(markerIndex + marker.Length))
                .Trim()
                .TrimStart('|')
                .Trim();
            return restoredFallback.Length == 0 ? null : restoredFallback;
        }

        private static bool IsSuccessfulPaymentStatus(string? status) =>
            string.Equals(status, "PAID", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(status, "success", StringComparison.OrdinalIgnoreCase) ||
            (status?.Contains("PAID", StringComparison.OrdinalIgnoreCase) ?? false) ||
            (status?.Contains("success", StringComparison.OrdinalIgnoreCase) ?? false);

        private static bool IsCancelledPaymentStatus(string? status) =>
            string.Equals(status, "cancel", StringComparison.OrdinalIgnoreCase) ||
            (status?.Contains("cancel", StringComparison.OrdinalIgnoreCase) ?? false);

        private static bool IsCompletedSpaBooking(SpaBooking booking) =>
            booking.SpaStatus == "4" ||
            (booking.SpaStatus?.EndsWith("|4", StringComparison.Ordinal) ?? false) ||
            string.Equals(booking.SpaStatus, "Hoàn thành", StringComparison.OrdinalIgnoreCase);

        private static bool IsUnpaidSpaBooking(SpaBooking booking) =>
            string.Equals(booking.Status, "Chờ thanh toán", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(booking.Status, "pending", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(booking.Status, "Chưa thanh toán", StringComparison.OrdinalIgnoreCase);

        // GET: /Cashier/Order/Create (POS Screen)
        [HttpGet]
        public async Task<IActionResult> Create(string? orderId, string? status)
        {
            if (!string.IsNullOrEmpty(orderId) && IsSuccessfulPaymentStatus(status))
            {
                var order = await _context.Orders.Include(o => o.Customer).FirstOrDefaultAsync(o => o.OrderId == orderId);
                if (order != null && order.Status == "Chờ thanh toán")
                {
                    await UpdateOrderToPaidAsync(order);
                }
            }
            else if (!string.IsNullOrEmpty(orderId) && IsCancelledPaymentStatus(status))
            {
                await CancelPendingPaymentOrderAsync(
                    orderId,
                    "Khách hàng hủy thanh toán trực tuyến qua PayOS.");
            }
            return View();
        }

        // GET: /Cashier/Order/CreateAtCounter (POS At Counter Screen)
        [HttpGet]
        public async Task<IActionResult> CreateAtCounter(string? orderId, string? status)
        {
            if (!string.IsNullOrEmpty(orderId))
            {
                if (IsSuccessfulPaymentStatus(status))
                {
                    var order = await _context.Orders
                        .Include(item => item.Customer)
                        .FirstOrDefaultAsync(item => item.OrderId == orderId);
                    if (order != null && order.Status == "Chờ thanh toán")
                    {
                        await UpdateOrderToPaidAsync(order);
                    }
                }
                else if (IsCancelledPaymentStatus(status))
                {
                    await CancelPendingPaymentOrderAsync(
                        orderId,
                        "Khách hàng hủy thanh toán trực tuyến qua PayOS tại quầy.");
                }
            }
            return View();
        }

        // API: Tìm kiếm Khách hàng (SĐT, Tên KH, Tên Pet)
        [HttpGet]
        public async Task<IActionResult> SearchCustomers(string q)
        {
            if (string.IsNullOrWhiteSpace(q))
            {
                return Json(new { success = true, data = new object[0] });
            }

            var query = q.ToLower();

            var customers = await _context.Customers
                .AsNoTracking()
                .Where(c => c.Phone.Contains(query) || 
                            c.FullName.ToLower().Contains(query) || 
                            c.Pets.Any(p => p.Name.ToLower().Contains(query)))
                .Take(20)
                .Select(c => new
                {
                    c.CustomerId,
                    c.FullName,
                    c.Phone,
                    c.MembershipTier,
                    c.LoyaltyPoints,
                    Pets = c.Pets.Select(p => new { p.PetId, p.Name, p.Species, p.Weight }).ToList()
                })
                .ToListAsync();

            return Json(new { success = true, data = customers });
        }

        // API: Đăng ký nhanh Khách hàng & Pet
        [HttpPost]
        public async Task<IActionResult> QuickRegister([FromBody] PosQuickRegisterDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.CustomerName) || string.IsNullOrWhiteSpace(dto.Phone))
            {
                return Json(new { success = false, message = "Tên và Số điện thoại là bắt buộc." });
            }

            // Check if phone exists
            var existing = await _context.Customers.FirstOrDefaultAsync(c => c.Phone == dto.Phone);
            if (existing != null)
            {
                return Json(new { success = false, message = "Số điện thoại đã tồn tại trong hệ thống." });
            }

            var customer = new ManagePetStore.Models.Customer
            {
                FullName = dto.CustomerName,
                Phone = dto.Phone,
                MembershipTier = "Đồng", // Default tier
                LoyaltyPoints = 0,
                CreatedAt = DateTime.Now
            };

            _context.Customers.Add(customer);
            await _context.SaveChangesAsync(); // To get CustomerId

            if (!string.IsNullOrWhiteSpace(dto.PetName))
            {
                var pet = new Pet
                {
                    CustomerId = customer.CustomerId,
                    Name = dto.PetName,
                    Species = dto.PetType ?? "Chó",
                    Weight = 0, // Default weight
                    Status = "Active"
                };
                _context.Pets.Add(pet);
                await _context.SaveChangesAsync();
            }

            var newCustomer = await _context.Customers
                .Include(c => c.Pets)
                .Where(c => c.CustomerId == customer.CustomerId)
                .Select(c => new
                {
                    c.CustomerId,
                    c.FullName,
                    c.Phone,
                    c.MembershipTier,
                    c.LoyaltyPoints,
                    Pets = c.Pets.Select(p => new { p.PetId, p.Name, p.Species, p.Weight }).ToList()
                })
                .FirstOrDefaultAsync();

            return Json(new { success = true, data = newCustomer });
        }

        // API: Tìm kiếm Sản phẩm & Dịch vụ Spa
        [HttpGet]
        public async Task<IActionResult> SearchProducts(string q)
        {
            var query = q?.ToLower() ?? "";

            // Search Products
            var products = await _context.Products
                .AsNoTracking()
                .Where(p => !p.IsDeleted && (p.Name.ToLower().Contains(query) || p.Sku.ToLower().Contains(query)))
                .Take(20)
                .Select(p => new
                {
                    Type = "Product",
                    Id = p.Sku,
                    Name = p.Name,
                    Price = p.Price,
                    Stock = p.Stock
                })
                .ToListAsync();

            // Search Spa Services
            var spas = await _context.SpaServices
                .AsNoTracking()
                .Where(s => s.Active && s.Name.ToLower().Contains(query))
                .Take(20)
                .Select(s => new
                {
                    Type = "Spa",
                    Id = s.ServiceId.ToString(),
                    Name = s.Name,
                    Price = s.Price,
                    Stock = 999 // Unlimited for services
                })
                .ToListAsync();

            var combined = products.Concat(spas).OrderBy(x => x.Name).ToList();

            return Json(new { success = true, data = combined });
        }

        // API: Lấy danh sách toàn bộ Dịch vụ Spa
        [HttpGet]
        public async Task<IActionResult> GetAllSpas()
        {
            var spas = await _context.SpaServices
                .AsNoTracking()
                .Where(s => s.Active)
                .OrderBy(s => s.Name)
                .Select(s => new
                {
                    Type = "Spa",
                    Id = s.ServiceId.ToString(),
                    Name = s.Name,
                    Price = s.Price,
                    Stock = 999
                })
                .ToListAsync();

            return Json(new { success = true, data = spas });
        }

        // API: Lấy danh sách toàn bộ Sản phẩm
        [HttpGet]
        public async Task<IActionResult> GetAllProducts()
        {
            var products = await _context.Products
                .AsNoTracking()
                .Where(p => !p.IsDeleted && p.Stock > 0)
                .OrderBy(p => p.Name)
                .Select(p => new
                {
                    Type = "Product",
                    Id = p.Sku,
                    p.Name,
                    p.Price,
                    p.Stock
                })
                .ToListAsync();

            return Json(new { success = true, data = products });
        }

        // API: Lấy danh sách Groomer trong ngày
        [HttpGet]
        public async Task<IActionResult> GetGroomers(DateTime date)
        {
            var groomers = await _context.Users
                .AsNoTracking()
                .Include(u => u.Role)
                .Where(u => u.Status == "Active" && u.Role.RoleName == "service")
                .Select(u => new
                {
                    u.UserId,
                    u.FullName
                })
                .ToListAsync();

            return Json(new { success = true, data = groomers });
        }

        // API: Lấy danh sách Lịch Spa đã hoàn thành nhưng chưa thanh toán
        [HttpGet]
        public async Task<IActionResult> GetCompletedSpaBookings()
        {
            var activeQueueItems = await _context.SpaQueues.AsNoTracking().ToListAsync();

            var bookings = await _context.SpaBookings
                .AsNoTracking()
                .Include(b => b.Pet)
                .Include(b => b.Customer)
                .Include(b => b.Service)
                .Include(b => b.Groomer)
                .Where(b => (b.SpaStatus == "4" || b.SpaStatus.EndsWith("|4") || b.SpaStatus == "Hoàn thành")
                         && (b.Status == "Chờ thanh toán" || b.Status == "pending" || b.Status == "Chưa thanh toán")
                         && (b.Notes == null || !b.Notes.Contains("OD-"))) // Chưa liên kết đơn POS
                .OrderByDescending(b => b.DateTime)
                .ToListAsync();

            var validBookings = bookings
                .Where(b => !activeQueueItems.Any(q => q.PetName != null && b.Pet != null && q.PetName.Trim().Equals(b.Pet.Name.Trim(), StringComparison.OrdinalIgnoreCase) && q.ArrivalTime.Date == b.DateTime.Date && q.ArrivalTime.Hour == b.DateTime.Hour))
                .Select(b => new
                {
                    BookingId = b.BookingId,
                    CustomerId = b.CustomerId,
                    CustomerName = b.Customer?.FullName ?? "Khách hàng",
                    CustomerPhone = b.Customer?.Phone ?? "",
                    PetName = b.Pet?.Name ?? "Thú cưng",
                    PetId = b.PetId,
                    PetWeight = b.Pet?.Weight ?? 0,
                    ServiceName = b.Service?.Name ?? "Dịch vụ Spa",
                    ServiceId = b.ServiceId,
                    Price = b.Price,
                    GroomerId = b.GroomerId,
                    GroomerName = b.Groomer?.FullName ?? "Groomer",
                    DateTime = b.DateTime.ToString("dd/MM/yyyy HH:mm"),
                    HeldForHotel = _context.HotelBookings.Any(hotel =>
                        hotel.PetId == b.PetId &&
                        hotel.CustomerId == b.CustomerId &&
                        (hotel.Status == "Active" || hotel.Status == "Đang ở") &&
                        b.DateTime >= hotel.CheckInDate &&
                        (!hotel.CheckOutDate.HasValue || b.DateTime <= hotel.CheckOutDate.Value))
                })
                .ToList();

            return Json(new { success = true, data = validBookings });
        }

        [HttpGet]
        // [nam] Trả về các bảng kê lưu trú đã sẵn sàng để thu ngân tạo đơn thanh toán.
        public async Task<IActionResult> GetReadyHotelCheckouts()
        {
            var statements = await _context.HotelCheckoutStatements
                .AsNoTracking()
                .Where(statement => statement.Status == "ReadyForPayment" && statement.OrderId == null)
                .OrderBy(statement => statement.PreparedAt)
                .Select(statement => new
                {
                    HotelCheckoutId = statement.CheckoutStatementId,
                    statement.HotelBookingId,
                    CustomerId = statement.HotelBooking.CustomerId,
                    CustomerName = statement.HotelBooking.Customer.FullName,
                    CustomerPhone = statement.HotelBooking.Customer.Phone,
                    PetId = statement.HotelBooking.PetId,
                    PetName = statement.HotelBooking.Pet.Name,
                    PetWeight = statement.HotelBooking.Pet.Weight,
                    RoomTypeId = statement.HotelBooking.Cage.RoomTypeId,
                    RoomTypeName = statement.HotelBooking.Cage.RoomType.Type,
                    statement.HotelBooking.CageId,
                    Total = statement.TotalAmount,
                    PreparedAt = statement.PreparedAt.ToString("dd/MM/yyyy HH:mm"),
                    LinkedSpaBookingIds = statement.HotelBooking.SpaLinks.Select(link => link.SpaBookingId).ToList()
                })
                .ToListAsync();
            return Json(new { success = true, data = statements });
        }

        // API: Kiểm tra và áp dụng Voucher
        [HttpGet]
        public async Task<IActionResult> CheckVoucher(string code, decimal subtotal)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                return Json(new { success = false, message = "Vui lòng nhập mã giảm giá." });
            }

            var cleanCode = code.Trim().ToUpper();
            var voucher = await _context.Vouchers
                .AsNoTracking()
                .FirstOrDefaultAsync(v => v.Code == cleanCode && v.Status && v.ExpiryDate >= DateTime.Today);

            if (voucher == null)
            {
                // Hỗ trợ một số mã test nếu database trống
                if (cleanCode == "PET20" || cleanCode == "SALE20")
                {
                    if (subtotal < 200000)
                        return Json(new { success = false, message = "Đơn hàng tối thiểu 200.000đ để sử dụng voucher này." });
                    return Json(new { success = true, discount = 20000m, code = cleanCode });
                }
                if (cleanCode == "PET10")
                {
                    if (subtotal < 100000)
                        return Json(new { success = false, message = "Đơn hàng tối thiểu 100.000đ để sử dụng voucher này." });
                    return Json(new { success = true, discount = Math.Round(subtotal * 0.1m, 0), code = cleanCode });
                }

                return Json(new { success = false, message = "Mã giảm giá không tồn tại hoặc đã hết hạn." });
            }

            if (subtotal < voucher.MinOrder)
            {
                return Json(new { success = false, message = $"Giá trị đơn hàng chưa đạt mức tối thiểu {voucher.MinOrder:N0}đ." });
            }

            decimal discount = 0;
            if (voucher.Type.Equals("Percent", StringComparison.OrdinalIgnoreCase) || voucher.Type.Equals("Percentage", StringComparison.OrdinalIgnoreCase))
            {
                discount = Math.Round(subtotal * voucher.Value / 100m, 0);
            }
            else
            {
                discount = voucher.Value;
            }

            return Json(new { success = true, discount = discount, code = voucher.Code });
        }

        // API: Submit Order
        [HttpPost]
        public async Task<IActionResult> SubmitOrder([FromBody] PosSubmitOrderDto dto)
        {
            if (dto.CustomerId == 0 || dto.Items == null || !dto.Items.Any())
            {
                return Json(new { success = false, message = "Dữ liệu không hợp lệ." });
            }

            // 1. Kiểm tra số lượng hợp lệ
            if (dto.Items.Any(i => i.Quantity <= 0))
            {
                return Json(new { success = false, message = "Số lượng mặt hàng thanh toán phải lớn hơn 0." });
            }
            if (dto.Items.Any(item =>
                    item.Type != "Product" &&
                    item.Type != "Spa" &&
                    item.Type != "Hotel"))
            {
                return Json(new { success = false, message = "Hóa đơn chứa loại mặt hàng không được hỗ trợ." });
            }
            if (dto.PaymentMethod != "Tiền mặt" &&
                dto.PaymentMethod != "Thanh toán online" &&
                dto.PaymentMethod != "Tiền mặt + Online")
            {
                return Json(new { success = false, message = "Phương thức thanh toán không hợp lệ." });
            }
            if (dto.VoucherDiscount < 0)
            {
                return Json(new { success = false, message = "Số tiền giảm giá không hợp lệ." });
            }

            bool hasPendingServiceItem = dto.Items.Any(item =>
                item.Type == "Hotel" ||
                (item.Type == "Spa" && item.BookingId.HasValue));
            if (hasPendingServiceItem &&
                (dto.VoucherDiscount > 0 || !string.IsNullOrWhiteSpace(dto.VoucherCode)))
            {
                return Json(new
                {
                    success = false,
                    message = "Voucher không áp dụng cho Spa chờ thu hoặc dịch vụ lưu trú chuồng chờ thu."
                });
            }

            await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);

            var hotelItems = dto.Items
                .Where(item => item.Type == "Hotel")
                .ToList();
            var hotelCheckoutIds = hotelItems
                .Where(item => item.HotelCheckoutId.HasValue)
                .Select(item => item.HotelCheckoutId!.Value)
                .Distinct()
                .ToList();
            if (hotelItems.Count != hotelCheckoutIds.Count)
            {
                return Json(new { success = false, message = "Bảng kê chuồng bị thiếu hoặc trùng liên kết thanh toán." });
            }

            var hotelCheckouts = await _context.HotelCheckoutStatements
                .Include(statement => statement.HotelBooking)
                    .ThenInclude(booking => booking.Cage)
                .Where(statement => hotelCheckoutIds.Contains(statement.CheckoutStatementId))
                .ToDictionaryAsync(statement => statement.CheckoutStatementId);
            if (hotelCheckouts.Count != hotelCheckoutIds.Count ||
                hotelCheckouts.Values.Any(statement => statement.Status != "ReadyForPayment" || statement.OrderId != null || statement.HotelBooking.CustomerId != dto.CustomerId))
            {
                return Json(new { success = false, message = "Bảng kê chuồng không còn hợp lệ hoặc đã được thanh toán." });
            }
            if (hotelCheckoutIds.Any() && dto.PointsUsed > 0)
            {
                return Json(new { success = false, message = "Điểm thành viên chưa áp dụng cho hóa đơn có dịch vụ lưu trú chuồng." });
            }
            foreach (var item in hotelItems)
            {
                if (!item.HotelCheckoutId.HasValue || !hotelCheckouts.TryGetValue(item.HotelCheckoutId.Value, out var statement))
                    return Json(new { success = false, message = "Thiếu liên kết bảng kê chuồng." });
                item.Id = statement.HotelBooking.Cage.RoomTypeId.ToString();
                item.Quantity = 1;
                item.Price = statement.TotalAmount;
                item.Total = statement.TotalAmount;
            }

            // 2. Kiểm tra trùng lặp thanh toán lịch Spa
            var linkedBookingIds = dto.Items
                .Where(item => item.Type == "Spa" && item.BookingId.HasValue)
                .Select(item => item.BookingId!.Value)
                .Distinct()
                .ToList();
            if (dto.Items.Count(item => item.Type == "Spa" && item.BookingId.HasValue) != linkedBookingIds.Count)
            {
                return Json(new { success = false, message = "Một lịch Spa không thể xuất hiện nhiều lần trong cùng hóa đơn." });
            }

            var bookingsDict = await _context.SpaBookings
                .Where(booking => linkedBookingIds.Contains(booking.BookingId))
                .ToDictionaryAsync(booking => booking.BookingId);
            if (bookingsDict.Count != linkedBookingIds.Count ||
                bookingsDict.Values.Any(booking =>
                    booking.CustomerId != dto.CustomerId ||
                    !IsCompletedSpaBooking(booking) ||
                    !IsUnpaidSpaBooking(booking)))
            {
                return Json(new { success = false, message = "Lịch Spa không còn hợp lệ, chưa hoàn thành hoặc đã được thanh toán." });
            }

            foreach (var item in dto.Items.Where(item => item.Type == "Spa" && item.BookingId.HasValue))
            {
                var booking = bookingsDict[item.BookingId!.Value];
                item.Id = booking.ServiceId.ToString();
                item.Quantity = 1;
                item.Price = booking.Price;
                item.Total = booking.Price;
            }

            var requiredSpaIds = await _context.HotelStaySpaLinks
                .Where(link => hotelCheckoutIds.Contains(link.HotelBooking.CheckoutStatement!.CheckoutStatementId))
                .Select(link => link.SpaBookingId)
                .ToListAsync();
            if (requiredSpaIds.Except(linkedBookingIds).Any())
            {
                return Json(new { success = false, message = "Lượt lưu trú chuồng có Spa liên quan; vui lòng thu chung trong cùng hóa đơn." });
            }
            var spaLinkedToHotelIds = await _context.HotelStaySpaLinks
                .Where(link => linkedBookingIds.Contains(link.SpaBookingId))
                .Select(link => new { link.SpaBookingId, CheckoutId = link.HotelBooking.CheckoutStatement!.CheckoutStatementId })
                .ToListAsync();
            if (spaLinkedToHotelIds.Any(link => !hotelCheckoutIds.Contains(link.CheckoutId)))
            {
                return Json(new { success = false, message = "Spa thuộc lượt lưu trú phải được thanh toán cùng bảng kê chuồng." });
            }
            if (linkedBookingIds.Any())
            {
                var existingLinkedBookings = await _context.SpaBookings
                    .AsNoTracking()
                    .Where(b => linkedBookingIds.Contains(b.BookingId) && b.Notes != null && b.Notes.Contains("OD-"))
                    .Select(b => b.BookingId)
                    .ToListAsync();

                if (existingLinkedBookings.Any())
                {
                    return Json(new { success = false, message = $"Lịch hẹn Spa #{string.Join(", #", existingLinkedBookings)} đã được liên kết hóa đơn thanh toán trước đó." });
                }
            }

            var customer = await _context.Customers.FindAsync(dto.CustomerId);
            if (customer == null)
            {
                return Json(new { success = false, message = "Khách hàng không tồn tại." });
            }

            if (dto.PointsUsed < 0 || dto.PointsUsed > customer.LoyaltyPoints)
            {
                return Json(new { success = false, message = "Số điểm thành viên sử dụng không hợp lệ." });
            }

            var productSkus = dto.Items
                .Where(item => item.Type == "Product")
                .Select(item => item.Id)
                .Distinct()
                .ToList();
            var productsDict = await _context.Products
                .Where(product => productSkus.Contains(product.Sku) && !product.IsDeleted)
                .ToDictionaryAsync(product => product.Sku);
            if (productsDict.Count != productSkus.Count)
            {
                return Json(new { success = false, message = "Có sản phẩm không còn được kinh doanh." });
            }

            foreach (var item in dto.Items.Where(item => item.Type == "Product"))
            {
                var product = productsDict[item.Id];
                item.Price = product.Price;
                item.Total = product.Price * item.Quantity;
            }

            var newSpaItems = dto.Items
                .Where(item => item.Type == "Spa" && !item.BookingId.HasValue)
                .ToList();
            var newSpaServiceIds = new List<int>();
            foreach (var item in newSpaItems)
            {
                if (!int.TryParse(item.Id, out int serviceId))
                {
                    return Json(new { success = false, message = "Dịch vụ Spa không hợp lệ." });
                }
                newSpaServiceIds.Add(serviceId);
            }

            var spaServicesDict = await _context.SpaServices
                .Where(service => newSpaServiceIds.Contains(service.ServiceId) && service.Active)
                .ToDictionaryAsync(service => service.ServiceId);
            if (spaServicesDict.Count != newSpaServiceIds.Distinct().Count())
            {
                return Json(new { success = false, message = "Có dịch vụ Spa không còn hoạt động." });
            }

            foreach (var item in newSpaItems)
            {
                int serviceId = int.Parse(item.Id);
                item.Price = spaServicesDict[serviceId].Price;
                item.Total = item.Price * item.Quantity;
            }

            dto.TotalAmount = dto.Items.Sum(item => item.Price * item.Quantity);
            decimal discount = dto.VoucherDiscount + (dto.PointsUsed * 500);
            decimal totalAmount = dto.TotalAmount - discount;
            if (totalAmount < 0) totalAmount = 0;
            if (dto.PaymentMethod == "Thanh toán online" && dto.OnlineAmount != totalAmount)
            {
                return Json(new { success = false, message = "Số tiền thanh toán online không khớp tổng hóa đơn." });
            }
            if (dto.PaymentMethod == "Tiền mặt + Online" &&
                (dto.CashAmount < 0 ||
                 dto.OnlineAmount <= 0 ||
                 dto.CashAmount + dto.OnlineAmount != totalAmount))
            {
                return Json(new { success = false, message = "Số tiền mặt và online không khớp tổng hóa đơn." });
            }

            // Generate Order ID using orderCode pattern for PayOS compatibility
            long orderCode = 0;
            string newOrderId = "";
            var numericString = $"{DateTime.Now:MMddHHmmss}{Random.Shared.Next(10, 99)}";
            orderCode = long.Parse(numericString);
            newOrderId = $"OD-{orderCode}";

            // Keep points earned silent (10 points initialized, but not added to account immediately)
            int pointsEarned = 10;
            _context.Entry(customer).State = EntityState.Modified;

            bool hasOnlinePayment = dto.PaymentMethod == "Thanh toán online" || 
                                     (dto.PaymentMethod == "Tiền mặt + Online" && dto.OnlineAmount > 0);

            var order = new Order
            {
                OrderId = newOrderId,
                CustomerId = dto.CustomerId,
                Date = DateTime.Now,
                Subtotal = dto.TotalAmount,
                Discount = discount,
                Total = totalAmount,
                PaymentMethod = dto.PaymentMethod ?? "Tiền mặt",
                PointsEarned = pointsEarned,
                PointsRedeemed = dto.PointsUsed,
                Status = hasOnlinePayment ? "Chờ thanh toán" : "Chờ xử lý",
                OrderStatus = hasOnlinePayment ? 1 : 2,
                CancelReason = !string.IsNullOrWhiteSpace(dto.VoucherCode) ? $"VOUCHER:{dto.VoucherCode.Trim().ToUpper()}" : null
            };

            _context.Orders.Add(order);
            await _context.SaveChangesAsync();

            foreach (int checkoutId in hotelCheckoutIds)
            {
                int updatedStatements = await _context.HotelCheckoutStatements
                    .Where(statement =>
                        statement.CheckoutStatementId == checkoutId &&
                        statement.Status == "ReadyForPayment" &&
                        statement.OrderId == null)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(statement => statement.OrderId, order.OrderId)
                        .SetProperty(statement => statement.Status, "LinkedToOrder"));
                if (updatedStatements != 1)
                {
                    return Json(new
                    {
                        success = false,
                        message = "Bảng kê chuồng vừa được một giao dịch khác tiếp nhận. Vui lòng tải lại danh sách."
                    });
                }
            }

            var systemStockDetails = new List<StockMovementDetail>();

            // Optimize lookups to avoid N+1 database queries
            var petIds = dto.Items.Where(i => i.Type == "Spa" && i.PetId.HasValue).Select(i => i.PetId!.Value).Distinct().ToList();
            var petsDict = await _context.Pets
                .Where(p => petIds.Contains(p.PetId))
                .ToDictionaryAsync(p => p.PetId);

            // Process Items
            foreach (var item in dto.Items)
            {
                var orderItem = new OrderItem
                {
                    OrderId = order.OrderId,
                    Quantity = item.Quantity,
                    Price = item.Price,
                    IsCombo = false
                };

                if (item.Type == "Product")
                {
                    orderItem.ProductSku = item.Id;

                    // Reduce Stock
                    if (productsDict.TryGetValue(item.Id, out var product))
                    {
                        try 
                        {
                            await _inventoryBatchService.DeductStockFIFO(item.Id, item.Quantity);
                        }
                        catch (ManagePetStore.Exceptions.ServiceException)
                        {
                            // Fallback to manual deduction if batch service fails (e.g. not enough stock recorded in batches)
                            await _context.Products.Where(p => p.Sku == item.Id)
                                .ExecuteUpdateAsync(s => s.SetProperty(p => p.Stock, p => p.Stock - item.Quantity));
                        }
                        
                        systemStockDetails.Add(new StockMovementDetail
                        {
                            ProductSku = item.Id,
                            Quantity = item.Quantity,
                            CostPrice = 0
                        });
                    }
                }
                else if (item.Type == "Spa")
                {
                    orderItem.SpaServiceId = int.Parse(item.Id);

                    // Update Pet Weight if provided
                    if (item.PetId.HasValue && item.PetWeight.HasValue)
                    {
                        if (petsDict.TryGetValue(item.PetId.Value, out var pet))
                        {
                            pet.Weight = item.PetWeight.Value;
                        }
                    }

                    // Link existing SpaBooking
                    if (item.BookingId.HasValue)
                    {
                        if (bookingsDict.TryGetValue(item.BookingId.Value, out var booking))
                        {
                            booking.Status = hasOnlinePayment ? "Chờ thanh toán" : "Đã thanh toán";
                            booking.Notes = $"[POS {order.OrderId}] | Dịch vụ: {item.Name} " + (booking.Notes ?? "");
                            _context.Entry(booking).State = EntityState.Modified;
                        }
                    }
                    else if (item.PetId.HasValue && item.GroomerId.HasValue && item.AppointmentTime.HasValue)
                    {
                        var service = await _context.SpaServices.FindAsync(orderItem.SpaServiceId);
                        var spaBooking = new SpaBooking
                        {
                            CustomerId = customer.CustomerId,
                            PetId = item.PetId.Value,
                            GroomerId = item.GroomerId.Value,
                            ServiceId = orderItem.SpaServiceId ?? 0,
                            DateTime = item.AppointmentTime.Value,
                            Status = "pending",
                            SpaStatus = "Pending",
                            Price = item.Price,
                            Notes = $"[Tạo từ POS] Đơn hàng: {order.OrderId} - Dịch vụ: {item.Name}"
                        };
                        _context.SpaBookings.Add(spaBooking);
                    }
                }
                else if (item.Type == "Hotel" && item.HotelCheckoutId.HasValue)
                {
                    orderItem.RoomTypeId = int.Parse(item.Id);
                }

                _context.OrderItems.Add(orderItem);
            }

            if (systemStockDetails.Any())
            {
                int userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "1");
                await _stockMovementService.CreateSystemMovement(
                    systemUserId: userId,
                    type: "Xuất kho (Bán hàng POS)",
                    status: "Đã hoàn thành",
                    supplier: null,
                    totalValue: 0,
                    details: systemStockDetails
                );
            }

            await _context.SaveChangesAsync();
            await ManagePetStore.Services.Customer.CustomerRewardHelper.RecalculateCustomerPointsAndTierAsync(customer.CustomerId, _context);
            await transaction.CommitAsync();
            _context.ChangeTracker.Clear();

            if (hasOnlinePayment)
            {
                long onlinePayAmount = (long)totalAmount;
                if (dto.PaymentMethod == "Tiền mặt + Online")
                {
                    onlinePayAmount = (long)dto.OnlineAmount;
                }

                if (onlinePayAmount >= 1000)
                {
                    var host = $"{Request.Scheme}://{Request.Host}";
                    var paymentRequest = new CreatePaymentLinkRequest
                    {
                        OrderCode = orderCode,
                        Amount = onlinePayAmount,
                        Description = $"POS {orderCode}",
                        CancelUrl = dto.IsAtCounter ? $"{host}/Cashier/Order/CreateAtCounter?orderId={order.OrderId}&status=cancel" : $"{host}/Cashier/Order/Create?orderId={order.OrderId}&status=cancel",
                        ReturnUrl = dto.IsAtCounter ? $"{host}/Cashier/Order/CreateAtCounter?orderId={order.OrderId}&status=success" : $"{host}/Cashier/Order/Create?orderId={order.OrderId}&status=success",
                        Items = dto.Items.Select(item => new PaymentLinkItem
                        {
                            Name = item.Name,
                            Quantity = item.Quantity,
                            Price = (long)item.Price
                        }).ToList()
                    };

                    try
                    {
                        var paymentLinkResult = await _payOS.PaymentRequests.CreateAsync(paymentRequest);
                        return Json(new { success = true, orderId = order.OrderId, redirectUrl = paymentLinkResult.CheckoutUrl, qrCode = paymentLinkResult.CheckoutUrl });
                    }
                    catch (Exception ex)
                    {
                        if (hotelCheckoutIds.Any())
                        {
                            await CancelPendingPaymentOrderAsync(
                                order.OrderId,
                                $"Không thể khởi tạo thanh toán PayOS: {ex.Message}");
                        }
                        return Json(new { success = false, message = $"Lỗi kết nối PayOS: {ex.Message}" });
                    }
                }
            }
            
            return Json(new { success = true, orderId = order.OrderId, redirectUrl = "" });
        }

        // API: Check Payment Status for Polling
        [HttpGet]
        public async Task<IActionResult> CheckPaymentStatus(string orderId)
        {
            var order = await _context.Orders.FindAsync(orderId);
            if (order == null)
            {
                return Json(new { success = false, message = "Không tìm thấy đơn hàng." });
            }

            if (order.Status == "Chờ xử lý" || order.Status == "Đã thanh toán")
            {
                return Json(new { success = true, status = order.Status });
            }

            // Check PayOS status
            var parts = orderId.Split('-');
            if (parts.Length >= 2 && long.TryParse(parts[^1], out long orderCode))
            {
                try
                {
                    var paymentInfo = await _payOS.PaymentRequests.GetAsync(orderCode);
                    if (paymentInfo != null && paymentInfo.Status.ToString().ToUpper() == "PAID")
                    {
                        await UpdateOrderToPaidAsync(order);

                        return Json(new { success = true, status = "PAID" });
                    }
                }
                catch (Exception)
                {
                    // Ignore transient network errors
                }
            }

            return Json(new { success = true, status = order.Status });
        }

        // API: Lấy thông tin hóa đơn dưới dạng JSON để hiển thị trực tiếp lên POS modal
        [HttpGet]
        public async Task<IActionResult> GetInvoiceData(string orderId)
        {
            var order = await _context.Orders
                .AsNoTracking()
                .Include(o => o.Customer)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.ProductSkuNavigation)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.SpaService)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.RoomType)
                .FirstOrDefaultAsync(o => o.OrderId == orderId);

            if (order == null)
            {
                return Json(new { success = false, message = "Không tìm thấy hóa đơn." });
            }

            var spaBookings = await _context.SpaBookings
                .AsNoTracking()
                .Include(sb => sb.Pet)
                .Include(sb => sb.Groomer)
                .Include(sb => sb.Service)
                .Where(sb => sb.Notes != null && sb.Notes.Contains($"[POS {orderId}]"))
                .Select(sb => new {
                    ServiceName = sb.Service.Name,
                    PetName = sb.Pet.Name,
                    PetSpecies = sb.Pet.Species,
                    PetWeight = sb.Pet.Weight,
                    GroomerName = sb.Groomer.FullName,
                    DateTime = sb.DateTime.ToString("HH:mm - dd/MM/yyyy")
                })
                .ToListAsync();

            var hotelCheckouts = await _context.HotelCheckoutStatements
                .AsNoTracking()
                .Where(statement => statement.OrderId == orderId)
                .Select(statement => new
                {
                    statement.HotelBookingId,
                    PetName = statement.HotelBooking.Pet.Name,
                    CageId = statement.HotelBooking.CageId,
                    RoomType = statement.HotelBooking.Cage.RoomType.Type,
                    statement.DiscountAmount,
                    statement.TotalAmount,
                    Items = statement.Items.OrderBy(item => item.CheckoutItemId).Select(item => new
                    {
                        item.Description,
                        item.Amount
                    }).ToList()
                })
                .ToListAsync();

            var displayHotelCheckouts = hotelCheckouts.Select(statement => new
            {
                statement.HotelBookingId,
                statement.PetName,
                statement.CageId,
                RoomType = CageTerminology.ForDisplay(statement.RoomType),
                statement.DiscountAmount,
                statement.TotalAmount,
                Items = statement.Items.Select(item => new
                {
                    Description = CageTerminology.ForDisplay(item.Description),
                    item.Amount
                }).ToList()
            }).ToList();

            string? voucherCode = null;
            if (order.CancelReason != null && order.CancelReason.StartsWith("VOUCHER:"))
            {
                voucherCode = order.CancelReason.Substring(8);
            }

            return Json(new
            {
                success = true,
                orderId = order.OrderId,
                date = order.Date.ToString("dd/MM/yyyy HH:mm"),
                customerName = order.Customer.FullName,
                customerPhone = order.Customer.Phone,
                subtotal = order.Subtotal,
                discount = order.Discount,
                total = order.Total,
                paymentMethod = order.PaymentMethod,
                voucherCode = voucherCode,
                items = order.OrderItems.Select(oi => new {
                    name = oi.ProductSku != null
                        ? CageTerminology.ForDisplay(oi.ProductSkuNavigation?.Name ?? oi.ProductSku)
                        : oi.SpaServiceId != null
                            ? oi.SpaService?.Name ?? "Dịch vụ Spa"
                            : $"Chuồng - {oi.RoomType?.Type ?? "Loại chuồng lưu trú"}",
                    quantity = oi.Quantity,
                    price = oi.Price,
                    total = oi.Price * oi.Quantity
                }).ToList(),
                spaBookings = spaBookings,
                hotelCheckouts = displayHotelCheckouts
            });
        }

        // GET: /Cashier/Order/PrintInvoice
        [HttpGet]
        public async Task<IActionResult> PrintInvoice(string orderId)
        {
            var order = await _context.Orders
                .AsNoTracking()
                .Include(o => o.Customer)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.ProductSkuNavigation)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.SpaService)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.RoomType)
                .FirstOrDefaultAsync(o => o.OrderId == orderId);

            if (order == null)
            {
                return NotFound("Không tìm thấy đơn hàng.");
            }

            // Retrieve related SpaBookings for this order
            var spaBookings = await _context.SpaBookings
                .AsNoTracking()
                .Include(b => b.Pet)
                .Include(b => b.Groomer)
                .Include(b => b.Service)
                .Where(b => b.Notes != null && b.Notes.Contains(orderId))
                .ToListAsync();

            ViewBag.SpaBookings = spaBookings;

            return View(order);
        }
    }
}
