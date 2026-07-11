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
                .Include(c => c.Pets)
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

        // API: Submit Order
        [HttpPost]
        public async Task<IActionResult> SubmitOrder([FromBody] PosSubmitOrderDto dto)
        {
            if (dto.CustomerId == 0 || dto.Items == null || !dto.Items.Any())
            {
                return Json(new { success = false, message = "Dữ liệu không hợp lệ." });
            }

            var customer = await _context.Customers.FindAsync(dto.CustomerId);
            if (customer == null)
            {
                return Json(new { success = false, message = "Khách hàng không tồn tại." });
            }

            // Deduct Points
            int pointsUsed = dto.PointsUsed;
            decimal discount = pointsUsed * 500m;
            if (pointsUsed > 0)
            {
                if (customer.LoyaltyPoints < pointsUsed)
                {
                    return Json(new { success = false, message = "Điểm tích lũy của khách hàng không đủ." });
                }
                customer.LoyaltyPoints -= pointsUsed;
            }

            decimal totalAmount = dto.TotalAmount - discount;
            if (totalAmount < 0) totalAmount = 0;

            // Generate Order ID using orderCode pattern for PayOS compatibility
            long orderCode = 0;
            string newOrderId = "";
            var numericString = $"{DateTime.Now:MMddHHmmss}{Random.Shared.Next(10, 99)}";
            orderCode = long.Parse(numericString);
            newOrderId = $"OD-{orderCode}";

            // Calculate Points Earned (1% of actual payment)
            int pointsEarned = (int)(totalAmount / 100000) * 10;
            customer.LoyaltyPoints += pointsEarned;

            // Save changes to customer (points subtracted and added)
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
                PointsRedeemed = pointsUsed,
                Status = hasOnlinePayment ? "Chờ thanh toán" : "Chờ xử lý",
                OrderStatus = hasOnlinePayment ? 1 : 2 
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

                    // Create SpaBooking
                    if (item.PetId.HasValue && item.GroomerId.HasValue && item.AppointmentTime.HasValue)
                    {
                        var service = await _context.SpaServices.FindAsync(orderItem.SpaServiceId);
                        int duration = service?.DurationMinutes ?? 60;

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

                _context.OrderItems.Add(orderItem);
            }

            await _context.SaveChangesAsync();

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
                        CancelUrl = $"{host}/Cashier/Order/Create?status=cancel",
                        ReturnUrl = $"{host}/Cashier/Order/Create?orderId={order.OrderId}&status=success",
                        Items = dto.Items.Select(item => new PaymentLinkItem
                        {
                            Name = item.Name,
                            Quantity = item.Quantity,
                            Price = (long)item.Price
                        }).ToList()
                    };

                    var paymentLinkResult = await _payOS.PaymentRequests.CreateAsync(paymentRequest);
                    return Json(new { success = true, orderId = order.OrderId, redirectUrl = paymentLinkResult.CheckoutUrl, qrCode = paymentLinkResult.CheckoutUrl });
                }
            }
            
            // If Cash or online amount too small, just return success
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
