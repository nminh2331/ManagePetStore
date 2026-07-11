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

namespace ManagePetStore.Areas.Cashier.Controllers
{
    [Area("Cashier")]
    [Authorize(Roles = "cashier")]
    public class OrderController : Controller
    {
        private readonly PetStoreManagementContext _context;
        private readonly PayOSClient _payOS;

        public OrderController(PetStoreManagementContext context, PayOSClient payOS)
        {
            _context = context;
            _payOS = payOS;
        }

        // GET: /Cashier/Order/Create (POS Screen)
        [HttpGet]
        public async Task<IActionResult> Create(string? orderId, string? status)
        {
            if (!string.IsNullOrEmpty(orderId) && (status == "PAID" || status == "success"))
            {
                var order = await _context.Orders.Include(o => o.Customer).FirstOrDefaultAsync(o => o.OrderId == orderId);
                if (order != null && order.Status == "Chờ thanh toán")
                {
                    order.Status = "Chờ xử lý";
                    order.OrderStatus = 2;
                    _context.Entry(order).State = EntityState.Modified;
                    await _context.SaveChangesAsync();
                }
            }
            return View();
        }

        // GET: /Cashier/Order/CreateAtCounter (POS At Counter Screen)
        [HttpGet]
        public async Task<IActionResult> CreateAtCounter(string? orderId, string? status)
        {
            if (!string.IsNullOrEmpty(orderId))
            {
                var order = await _context.Orders.Include(o => o.Customer).FirstOrDefaultAsync(o => o.OrderId == orderId);
                if (order != null && order.Status == "Chờ thanh toán")
                {
                    if (status == "PAID" || status == "success")
                    {
                        order.Status = "Chờ xử lý";
                        order.OrderStatus = 2;
                        _context.Entry(order).State = EntityState.Modified;
                        await _context.SaveChangesAsync();
                    }
                    else if (status == "cancel")
                    {
                        order.Status = "Đã hủy";
                        order.OrderStatus = 0; // Canceled
                        order.CancelReason = "Khách hàng hủy thanh toán trực tuyến qua PayOS tại quầy.";
                        _context.Entry(order).State = EntityState.Modified;
                        await _context.SaveChangesAsync();
                        if (order.CustomerId != null)
                        {
                            await ManagePetStore.Services.Customer.CustomerRewardHelper.RecalculateCustomerPointsAndTierAsync(order.CustomerId, _context);
                        }
                    }
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
            var bookings = await _context.SpaBookings
                .AsNoTracking()
                .Where(b => (b.SpaStatus == "4" || b.SpaStatus.EndsWith("|4"))
                         && (b.Status == "Chờ thanh toán" || b.Status == "pending" || b.Status == "Chưa thanh toán")
                         && (b.Notes == null || !b.Notes.Contains("OD-"))) // Chưa liên kết đơn POS
                .OrderByDescending(b => b.DateTime)
                .Select(b => new
                {
                    BookingId = b.BookingId,
                    CustomerId = b.CustomerId,
                    CustomerName = b.Customer.FullName,
                    CustomerPhone = b.Customer.Phone,
                    PetName = b.Pet.Name,
                    PetId = b.PetId,
                    PetWeight = b.Pet.Weight,
                    ServiceName = b.Service.Name,
                    ServiceId = b.ServiceId,
                    Price = b.Price,
                    GroomerId = b.GroomerId,
                    GroomerName = b.Groomer.FullName,
                    DateTime = b.DateTime.ToString("dd/MM/yyyy HH:mm"),
                    HeldForHotel = _context.HotelBookings.Any(hotel =>
                        hotel.PetId == b.PetId &&
                        hotel.CustomerId == b.CustomerId &&
                        (hotel.Status == "Active" || hotel.Status == "Đang ở") &&
                        b.DateTime >= hotel.CheckInDate &&
                        (!hotel.CheckOutDate.HasValue || b.DateTime <= hotel.CheckOutDate.Value))
                })
                .ToListAsync();

            return Json(new { success = true, data = bookings });
        }

        [HttpGet]
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

            var hotelCheckoutIds = dto.Items
                .Where(item => item.Type == "Hotel" && item.HotelCheckoutId.HasValue)
                .Select(item => item.HotelCheckoutId!.Value)
                .Distinct()
                .ToList();
            var hotelCheckouts = await _context.HotelCheckoutStatements
                .Include(statement => statement.HotelBooking)
                    .ThenInclude(booking => booking.Cage)
                .Where(statement => hotelCheckoutIds.Contains(statement.CheckoutStatementId))
                .ToDictionaryAsync(statement => statement.CheckoutStatementId);
            if (hotelCheckouts.Count != hotelCheckoutIds.Count ||
                hotelCheckouts.Values.Any(statement => statement.Status != "ReadyForPayment" || statement.OrderId != null || statement.HotelBooking.CustomerId != dto.CustomerId))
            {
                return Json(new { success = false, message = "Bảng kê Hotel không còn hợp lệ hoặc đã được thanh toán." });
            }
            if (hotelCheckoutIds.Any() && dto.VoucherDiscount > 0)
            {
                return Json(new { success = false, message = "Voucher POS chưa áp dụng cho hóa đơn có dịch vụ Hotel." });
            }
            foreach (var item in dto.Items.Where(item => item.Type == "Hotel"))
            {
                if (!item.HotelCheckoutId.HasValue || !hotelCheckouts.TryGetValue(item.HotelCheckoutId.Value, out var statement))
                    return Json(new { success = false, message = "Thiếu liên kết bảng kê Hotel." });
                item.Id = statement.HotelBooking.Cage.RoomTypeId.ToString();
                item.Quantity = 1;
                item.Price = statement.TotalAmount;
                item.Total = statement.TotalAmount;
            }
            dto.TotalAmount = dto.Items.Sum(item => item.Price * item.Quantity);

            // 2. Kiểm tra trùng lặp thanh toán lịch Spa
            var linkedBookingIds = dto.Items.Where(i => i.BookingId.HasValue).Select(i => i.BookingId!.Value).ToList();
            var requiredSpaIds = await _context.HotelStaySpaLinks
                .Where(link => hotelCheckoutIds.Contains(link.HotelBooking.CheckoutStatement!.CheckoutStatementId))
                .Select(link => link.SpaBookingId)
                .ToListAsync();
            if (requiredSpaIds.Except(linkedBookingIds).Any())
            {
                return Json(new { success = false, message = "Lượt Hotel có Spa liên quan; vui lòng thu chung trong cùng hóa đơn." });
            }
            var spaLinkedToHotelIds = await _context.HotelStaySpaLinks
                .Where(link => linkedBookingIds.Contains(link.SpaBookingId))
                .Select(link => new { link.SpaBookingId, CheckoutId = link.HotelBooking.CheckoutStatement!.CheckoutStatementId })
                .ToListAsync();
            if (spaLinkedToHotelIds.Any(link => !hotelCheckoutIds.Contains(link.CheckoutId)))
            {
                return Json(new { success = false, message = "Spa thuộc lượt lưu trú phải được thanh toán cùng bảng kê Hotel." });
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

            decimal discount = dto.VoucherDiscount + (dto.PointsUsed * 500);
            decimal totalAmount = dto.TotalAmount - discount;
            if (totalAmount < 0) totalAmount = 0;

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
                    var product = await _context.Products.FirstOrDefaultAsync(p => p.Sku == item.Id);
                    if (product != null)
                    {
                        product.Stock -= item.Quantity;
                        if (product.Stock < 0) product.Stock = 0;
                    }
                }
                else if (item.Type == "Spa")
                {
                    orderItem.SpaServiceId = int.Parse(item.Id);

                    // Update Pet Weight if provided
                    if (item.PetId.HasValue && item.PetWeight.HasValue)
                    {
                        var pet = await _context.Pets.FindAsync(item.PetId.Value);
                        if (pet != null)
                        {
                            pet.Weight = item.PetWeight.Value;
                        }
                    }

                    // Link existing SpaBooking
                    if (item.BookingId.HasValue)
                    {
                        var booking = await _context.SpaBookings.FindAsync(item.BookingId.Value);
                        if (booking != null)
                        {
                            booking.Status = "Chờ thanh toán";
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
                    var checkout = hotelCheckouts[item.HotelCheckoutId.Value];
                    checkout.OrderId = order.OrderId;
                    checkout.Status = "LinkedToOrder";
                }

                _context.OrderItems.Add(orderItem);
            }

            await _context.SaveChangesAsync();
            await ManagePetStore.Services.Customer.CustomerRewardHelper.RecalculateCustomerPointsAndTierAsync(customer.CustomerId, _context);

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
                        CancelUrl = dto.IsAtCounter ? $"{host}/Cashier/Order/CreateAtCounter?orderId={order.OrderId}&status=cancel" : $"{host}/Cashier/Order/Create?status=cancel",
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
                        order.Status = "Chờ xử lý";
                        order.OrderStatus = 2;
                        _context.Entry(order).State = EntityState.Modified;
                        await _context.SaveChangesAsync();

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
                    statement.TotalAmount,
                    Items = statement.Items.OrderBy(item => item.CheckoutItemId).Select(item => new
                    {
                        item.Description,
                        item.Amount
                    }).ToList()
                })
                .ToListAsync();

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
                        ? oi.ProductSkuNavigation?.Name ?? oi.ProductSku
                        : oi.SpaServiceId != null
                            ? oi.SpaService?.Name ?? "Dịch vụ Spa"
                            : $"Hotel - {oi.RoomType?.Type ?? "Phòng lưu trú"}",
                    quantity = oi.Quantity,
                    price = oi.Price,
                    total = oi.Price * oi.Quantity
                }).ToList(),
                spaBookings = spaBookings,
                hotelCheckouts = hotelCheckouts
            });
        }

        // GET: /Cashier/Order/PrintInvoice
        [HttpGet]
        public async Task<IActionResult> PrintInvoice(string orderId)
        {
            var order = await _context.Orders
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
