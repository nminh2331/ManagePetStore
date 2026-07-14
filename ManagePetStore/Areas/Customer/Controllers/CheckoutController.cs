// hà hoàng hiệp code 
using System.Security.Claims;
using ManagePetStore.Areas.Customer.Models;
using ManagePetStore.Services.Customer;
using ManagePetStore.Services;
using ManagePetStore.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;
using PayOS;
using PayOS.Models;
using PayOS.Models.V2.PaymentRequests;
using ManagePetStore.Services.Warehouse;

namespace ManagePetStore.Areas.Customer.Controllers;

[Area("Customer")]
[Authorize]
public class CheckoutController : Controller
{
    private readonly ICartService _cartService;
    private readonly PetStoreManagementContext _context;
    private readonly ICheckoutEmailService _checkoutEmailService;
    private readonly PayOSClient _payOS;
    private readonly IStockMovementService _stockMovementService;
    private readonly IInventoryBatchService _inventoryBatchService;

    public CheckoutController(
        ICartService cartService,
        PetStoreManagementContext context,
        ICheckoutEmailService checkoutEmailService,
        PayOSClient payOS,
        IStockMovementService stockMovementService,
        IInventoryBatchService inventoryBatchService)
    {
        _cartService = cartService;
        _context = context;
        _checkoutEmailService = checkoutEmailService;
        _payOS = payOS;
        _stockMovementService = stockMovementService;
        _inventoryBatchService = inventoryBatchService;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var cart = await _cartService.GetCartPageAsync(); // Lấy cart
        if (!cart.Items.Any())
        {
            TempData["ErrorMessage"] = "Giỏ hàng trống. Vui lòng thêm sản phẩm trước khi thanh toán.";
            return RedirectToAction("Index", "Cart");
        }

        var customer = await GetCurrentCustomerAsync();  //Lấy khách hàng hiện tại


        if (customer == null)
        {
            TempData["ErrorMessage"] = "Không tìm thấy thông tin khách hàng. Vui lòng đăng nhập tài khoản khách hàng.";
            return RedirectToAction("Index", "Cart");
        }

        var model = new CheckoutViewModel  //Tạo model cho view


        {
            FullName = customer.FullName,
            Phone = customer.Phone,
            Email = customer.Email ?? "",
            Cart = cart,
            PaymentMethod = "Cash"
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApplyVoucher(string voucherCode)
    {
        var (success, message) = await _cartService.ApplyVoucherAsync(voucherCode);
        TempData[success ? "SuccessMessage" : "ErrorMessage"] = message;
        return RedirectToAction(nameof(Index));
    }


    //Đây là action chốt đơn thật sự.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Confirm(
        string fullName,
        string phone,
        string email,
        string shippingAddress,
        string? orderNote,
        string paymentMethod)
    {
        var trimmedFullName = fullName?.Trim() ?? string.Empty;
        var trimmedPhone = phone?.Trim() ?? string.Empty;
        var trimmedEmail = email?.Trim() ?? string.Empty;
        var trimmedShippingAddress = shippingAddress?.Trim() ?? string.Empty;
        //Kiểm tra lại cart
        var cart = await _cartService.GetCartPageAsync();
        if (!cart.Items.Any())
        {
            TempData["ErrorMessage"] = "Giỏ hàng trống.";
            return RedirectToAction("Index", "Cart");
        }
        //Kiểm tra customer lần nữa
        var customer = await GetCurrentCustomerAsync();
        if (customer == null)
        {
            TempData["ErrorMessage"] = "Không tìm thấy thông tin khách hàng.";
            return RedirectToAction("Index", "Cart");
        }

        if (string.IsNullOrWhiteSpace(trimmedFullName) ||
            string.IsNullOrWhiteSpace(trimmedPhone) ||
            string.IsNullOrWhiteSpace(trimmedEmail) ||
            string.IsNullOrWhiteSpace(trimmedShippingAddress))
        {
            TempData["ErrorMessage"] = "Vui lòng nhập đầy đủ họ tên, số điện thoại, Gmail và địa chỉ giao hàng.";
            return RedirectToAction(nameof(Index));
        }

        var fullNameRegex = new Regex(@"^[\p{L}\s]+$");
        if (!fullNameRegex.IsMatch(trimmedFullName))
        {
            TempData["ErrorMessage"] = "Họ tên chỉ được chứa chữ và khoảng trắng, không được nhập số hoặc ký tự khác.";
            return RedirectToAction(nameof(Index));
        }

        var phoneRegex = new Regex(@"^\d{10}$");
        if (!phoneRegex.IsMatch(trimmedPhone))
        {
            TempData["ErrorMessage"] = "Số điện thoại phải là 10 chữ số và không được chứa ký tự đặc biệt.";
            return RedirectToAction(nameof(Index));
        }

        var emailRegex = new Regex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$");
        if (!emailRegex.IsMatch(trimmedEmail))
        {
            TempData["ErrorMessage"] = "Gmail không đúng định dạng. Vui lòng kiểm tra lại.";
            return RedirectToAction(nameof(Index));
        }

        var normalizedPayment = NormalizePaymentMethod(paymentMethod);
        if (normalizedPayment == null)
        {
            TempData["ErrorMessage"] = "Phương thức thanh toán không hợp lệ.";
            return RedirectToAction(nameof(Index));
        }

        Wallet? customerWallet = null;
        if (normalizedPayment == "Ví điện tử")
        {
            customerWallet = await _context.Wallets.FirstOrDefaultAsync(w => w.CustomerId == customer.CustomerId);
            if (customerWallet == null || customerWallet.Balance < cart.GrandTotal)
            {
                TempData["ErrorMessage"] = "Số dư ví điện tử không đủ để thanh toán. Vui lòng chọn phương thức khác hoặc nạp thêm tiền.";
                return RedirectToAction(nameof(Index));
            }
        }

        long orderCode = 0;
        string orderId;
        if (normalizedPayment == "Thanh toán online")
        {
            var numericString = $"{DateTime.Now:MMddHHmmss}{Random.Shared.Next(10, 99)}";
            orderCode = long.Parse(numericString);
            orderId = $"ORD-{orderCode}";
        }
        else
        {
            orderId = $"ORD-{DateTime.Now:yyyyMMddHHmmss}-{Random.Shared.Next(1000, 9999)}";
        }
        var status = (normalizedPayment == "Tiền mặt" || normalizedPayment == "Ví điện tử") ? "Chờ xử lý" : "Chờ thanh toán";

        try
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();
            //Tạo entity Order
            var order = new Order
            {
                OrderId = orderId,
                CustomerId = customer.CustomerId,
                Subtotal = cart.Subtotal,
                Discount = cart.VoucherDiscount,
                Total = cart.GrandTotal,
                PaymentMethod = normalizedPayment,
                PointsRedeemed = 0,
                PointsEarned = 10,
                Status = status,
                Date = DateTime.Now
            };

            _context.Orders.Add(order);  //Đưa entity vào change tracker, chưa save ngay.

            var systemStockDetails = new List<StockMovementDetail>();

            foreach (var item in cart.Items)
            {
                var isSpa = item.Sku.StartsWith("SPA-SVC-", StringComparison.OrdinalIgnoreCase);
                int? spaServiceIdVal = null;

                if (isSpa)
                {
                    var serviceIdString = item.Sku.Substring(8);
                    if (int.TryParse(serviceIdString, out int serviceId))
                    {
                        spaServiceIdVal = serviceId;

                        var pet = await _context.Pets.FirstOrDefaultAsync(p => p.CustomerId == customer.CustomerId);
                        int petId;
                        if (pet == null)
                        {
                            var petName = "Pet của " + customer.FullName;
                            if (petName.Length > 50)
                            {
                                petName = petName.Substring(0, 50);
                            }

                            pet = new Pet
                            {
                                CustomerId = customer.CustomerId,
                                Name = petName,
                                Species = "Chó/Mèo",
                                Breed = "Chưa xác định",
                                Age = "1 tuổi",
                                Weight = 5.0m,
                                Status = "Active"
                            };
                            _context.Pets.Add(pet);
                            await _context.SaveChangesAsync();
                        }
                        petId = pet.PetId;

                        var spaService = await _context.SpaServices.FirstOrDefaultAsync(s => s.ServiceId == serviceId);
                        if (spaService != null)
                        {
                            var activeGroomers = await _context.Users
                                .Where(u => u.RoleId == 3 && u.Status == "Active")
                                .ToListAsync();
                            if (!activeGroomers.Any())
                            {
                                var fallbackGroomer = await _context.Users.FirstOrDefaultAsync(u => u.RoleId == 3)
                                                     ?? await _context.Users.FirstOrDefaultAsync();
                                if (fallbackGroomer != null)
                                {
                                    activeGroomers.Add(fallbackGroomer);
                                }
                            }

                            int finalGroomerId = activeGroomers.FirstOrDefault()?.UserId ?? 3;
                            DateTime finalBookingTime = DateTime.Today.AddHours(9);
                            if (DateTime.Now >= finalBookingTime)
                            {
                                finalBookingTime = DateTime.Today.AddDays(1).AddHours(9);
                            }

                            // Ca làm việc khả dụng: 08:00, 09:00, 10:00, 11:00, 13:00, 14:00, 15:00, 16:00
                            int[] availableHours = { 8, 9, 10, 11, 13, 14, 15, 16 };
                            bool foundSlot = false;
                            
                            // Thử tìm trong 7 ngày tới để có ca rảnh thực tế
                            for (int dayOffset = 0; dayOffset < 7 && !foundSlot; dayOffset++)
                            {
                                var testDate = DateTime.Today.AddDays(dayOffset);
                                if (dayOffset == 0 && DateTime.Now.Hour >= 16)
                                {
                                    continue;
                                }

                                foreach (var hour in availableHours)
                                {
                                    var testDateTime = testDate.AddHours(hour);
                                    if (testDateTime <= DateTime.Now)
                                    {
                                        continue;
                                    }

                                    foreach (var groomer in activeGroomers)
                                    {
                                        var bookingsOnDay = await _context.SpaBookings
                                            .Include(b => b.Service)
                                            .Where(b => b.GroomerId == groomer.UserId 
                                                     && b.DateTime.Date == testDate 
                                                     && b.SpaStatus != "Cancelled")
                                            .ToListAsync();

                                        bool isOverlapTest = bookingsOnDay.Any(b => {
                                            var startE = b.DateTime;
                                            var endE = b.DateTime.AddMinutes(b.Service?.DurationMinutes ?? 30);
                                            var startN = testDateTime;
                                            var endN = testDateTime.AddMinutes(spaService.DurationMinutes);
                                            return startN < endE && startE < endN;
                                        });

                                        if (!isOverlapTest)
                                        {
                                            finalGroomerId = groomer.UserId;
                                            finalBookingTime = testDateTime;
                                            foundSlot = true;
                                            break;
                                        }
                                    }

                                    if (foundSlot) break;
                                }
                            }

                            var bookingStatus = (normalizedPayment == "Tiền mặt") ? "Chưa thanh toán" : "Đã thanh toán";

                            var spaBooking = new SpaBooking
                            {
                                PetId = petId,
                                CustomerId = customer.CustomerId,
                                ServiceId = serviceId,
                                DateTime = finalBookingTime,
                                GroomerId = finalGroomerId,
                                Price = spaService.Price,
                                Status = bookingStatus,
                                SpaStatus = "|0",
                                Notes = string.IsNullOrEmpty(orderNote) ? "Đặt lịch trực tuyến qua đơn hàng " + orderId : orderNote.Trim()
                            };
                            _context.SpaBookings.Add(spaBooking);
                        }
                    }
                }
                else
                {
                    await EnsureProductForOrderItemAsync(item);
                }
                //Tạo OrderItem

                _context.OrderItems.Add(new OrderItem
                {
                    OrderId = orderId,
                    ProductSku = isSpa ? null : item.Sku,
                    SpaServiceId = spaServiceIdVal,
                    Quantity = item.Quantity,
                    Price = item.UnitPrice,
                    IsCombo = false
                });

                if (!isSpa && !string.IsNullOrEmpty(item.Sku))
                {
                    systemStockDetails.Add(new StockMovementDetail
                    {
                        ProductSku = item.Sku,
                        Quantity = item.Quantity,
                        CostPrice = 0 // Not tracking cost for export right now
                    });
                }
            }


            string? payosCheckoutUrl = null;
            if (normalizedPayment == "Thanh toán online")
            {
                var host = $"{Request.Scheme}://{Request.Host}";
                var paymentRequest = new CreatePaymentLinkRequest
                {
                    OrderCode = orderCode,
                    Amount = (long)cart.GrandTotal,
                    Description = $"PetStore {orderCode}",
                    CancelUrl = $"{host}/Customer/Checkout/Index",
                    ReturnUrl = $"{host}/Customer/Checkout/Success?orderId={orderId}",
                    Items = cart.Items.Select(item => new PaymentLinkItem
                    {
                        Name = item.Name,
                        Quantity = item.Quantity,
                        Price = (long)item.UnitPrice
                    }).ToList()
                };

                var paymentLinkResult = await _payOS.PaymentRequests.CreateAsync(paymentRequest);
                payosCheckoutUrl = paymentLinkResult.CheckoutUrl;
            }

            // Loyalty points will be calculated when the order is completed.
            _context.Entry(customer).State = EntityState.Modified;

            if (normalizedPayment == "Ví điện tử" && customerWallet != null)
            {
                customerWallet.Balance -= cart.GrandTotal;
                customerWallet.UpdatedAt = DateTime.Now;
                _context.Entry(customerWallet).State = EntityState.Modified;

                _context.WalletTransactions.Add(new WalletTransaction
                {
                    WalletId = customerWallet.WalletId,
                    Amount = -cart.GrandTotal,
                    Type = "Payment",
                    Description = $"Thanh toán đơn hàng {orderId}",
                    OrderId = orderId,
                    TransactionDate = DateTime.Now
                });
            }

            if (systemStockDetails.Any())
            {
                await _stockMovementService.CreateSystemMovement(
                    systemUserId: 1, // Admin ID as system
                    type: "Xuất kho (Bán hàng online)",
                    status: "Đã hoàn thành",
                    supplier: null,
                    totalValue: cart.GrandTotal,
                    details: systemStockDetails
                );
            }

            await _context.SaveChangesAsync();
            await ManagePetStore.Services.Customer.CustomerRewardHelper.RecalculateCustomerPointsAndTierAsync(customer.CustomerId, _context);
            await transaction.CommitAsync();

            if (normalizedPayment != "Thanh toán online")
            {
                _cartService.ClearCart();
            }

            if (normalizedPayment == "Thanh toán online")
            {
                if (!string.IsNullOrWhiteSpace(orderNote))
                {
                    TempData["OrderNote"] = orderNote.Trim();
                    HttpContext.Session.SetString("OrderNote", orderNote.Trim());
                }
                // Store cart items in session so email can be sent after PayOS confirms payment
                var cartItemsJson = System.Text.Json.JsonSerializer.Serialize(cart.Items.ToList());
                HttpContext.Session.SetString($"CheckoutCartItems_{orderId}", cartItemsJson);
                // Store success model so Success page can read it when coming from PayOS
                var payosSuccessModel = new CheckoutSuccessViewModel
                {
                    OrderId = orderId,
                    FullName = trimmedFullName,
                    Phone = trimmedPhone,
                    ShippingAddress = trimmedShippingAddress,
                    ConfirmationEmail = trimmedEmail,
                    PaymentMethod = normalizedPayment,
                    Total = cart.GrandTotal,
                    ItemCount = cart.TotalQuantity
                };
                HttpContext.Session.SetString("CheckoutSuccess", System.Text.Json.JsonSerializer.Serialize(payosSuccessModel));

                if (!string.IsNullOrEmpty(payosCheckoutUrl))
                {
                    return Redirect(payosCheckoutUrl);
                }
            }

            var successModel = new CheckoutSuccessViewModel
            {
                OrderId = orderId,
                FullName = trimmedFullName,
                Phone = trimmedPhone,
                ShippingAddress = trimmedShippingAddress,
                ConfirmationEmail = trimmedEmail,
                PaymentMethod = normalizedPayment,
                Total = cart.GrandTotal,
                ItemCount = cart.TotalQuantity
            };

            //Gửi email xác nhận
            try
            {
                await _checkoutEmailService.SendOrderConfirmationAsync(
                    trimmedEmail,
                    successModel,
                    cart.Items,
                    orderNote);

                TempData["EmailSentMessage"] = $"Email xác nhận đơn hàng đã được gửi đến {trimmedEmail}.";
            }
            catch
            {
                TempData["EmailSentWarning"] = "Đơn hàng đã tạo thành công nhưng không gửi được email xác nhận. Vui lòng kiểm tra cấu hình Gmail trong appsettings.json.";
            }

            //Lưu dữ liệu success bằng TempData
            var successJson = System.Text.Json.JsonSerializer.Serialize(successModel);
            TempData["CheckoutSuccess"] = successJson;
            HttpContext.Session.SetString("CheckoutSuccess", successJson);

            if (!string.IsNullOrWhiteSpace(orderNote))
            {
                TempData["OrderNote"] = orderNote.Trim();
            }

            return RedirectToAction(nameof(Success));
        }
        catch (Exception)
        {
            TempData["ErrorMessage"] = "Không thể tạo đơn hàng. Vui lòng thử lại sau.";
            return RedirectToAction(nameof(Index));
        }
    }

    [HttpGet]
    public async Task<IActionResult> Success(string? orderId)
    {
        // Try TempData first (Cash flow), then Session (PayOS flow)
        var json = TempData["CheckoutSuccess"] as string ?? HttpContext.Session.GetString("CheckoutSuccess");
        CheckoutSuccessViewModel? model = null;

        if (!string.IsNullOrEmpty(json))
        {
            model = System.Text.Json.JsonSerializer.Deserialize<CheckoutSuccessViewModel>(json);
        }

        if (!string.IsNullOrEmpty(orderId))
        {
            var order = await _context.Orders
                .Include(o => o.OrderItems)
                .Include(o => o.Customer)
                .FirstOrDefaultAsync(o => o.OrderId == orderId);

            if (order != null)
            {
                var customer = order.Customer;

                if (order.PaymentMethod == "Thanh toán online" && order.Status == "Chờ thanh toán")
                {
                    var parts = orderId.Split('-');
                    if (parts.Length >= 2 && long.TryParse(parts[^1], out long orderCode))
                    {
                        try
                        {
                            bool isPaid = false;
                            string? payOsStatus = Request.Query["status"];
                            string? payOsCancel = Request.Query["cancel"];

                            if (payOsCancel == "true" || payOsStatus == "CANCELLED")
                            {
                                // Hoàn lại tồn kho vì thanh toán bị hủy
                                await RestoreStockForOrderAsync(order);
                                order.Status = "Đã hủy";
                                _context.Entry(order).State = EntityState.Modified;
                                await _context.SaveChangesAsync();

                                TempData["ErrorMessage"] = "Giao dịch thanh toán đã bị hủy. Tồn kho đã được hoàn trả.";
                                return RedirectToAction(nameof(Index));
                            }

                            if (payOsStatus == "PAID")
                            {
                                isPaid = true;
                            }
                            else
                            {
                                var paymentInfo = await _payOS.PaymentRequests.GetAsync(orderCode);
                                if (paymentInfo != null && paymentInfo.Status.ToString().ToUpper() == "PAID")
                                {
                                    isPaid = true;
                                }
                            }

                            if (isPaid)
                            {
                                // Update database order status
                                order.Status = "Chờ xử lý";
                                _context.Entry(order).State = EntityState.Modified;
                                await _context.SaveChangesAsync();

                                // Clear the cart now since payment succeeded
                                _cartService.ClearCart();

                                // Send order confirmation email
                                var itemsJson = HttpContext.Session.GetString($"CheckoutCartItems_{orderId}");
                                var orderNote = TempData["OrderNote"] as string ?? HttpContext.Session.GetString("OrderNote") ?? "";
                                var tempSuccessModel = new CheckoutSuccessViewModel
                                {
                                    OrderId = order.OrderId,
                                    FullName = customer.FullName,
                                    Phone = customer.Phone,
                                    ShippingAddress = "",
                                    ConfirmationEmail = customer.Email ?? "",
                                    PaymentMethod = order.PaymentMethod,
                                    Total = order.Total,
                                    ItemCount = order.OrderItems.Sum(i => i.Quantity)
                                };

                                if (!string.IsNullOrEmpty(itemsJson))
                                {
                                    try
                                    {
                                        var items = System.Text.Json.JsonSerializer.Deserialize<List<CartLineItemViewModel>>(itemsJson);
                                        if (items != null)
                                        {
                                            await _checkoutEmailService.SendOrderConfirmationAsync(
                                                customer.Email ?? "",
                                                tempSuccessModel,
                                                items,
                                                orderNote);
                                            ViewBag.EmailSentMessage = $"Email xác nhận đơn hàng đã được gửi đến {customer.Email}.";
                                        }
                                    }
                                    catch
                                    {
                                        // Ignore email error
                                    }
                                }
                            }
                            else
                            {
                                // Thanh toán không hoàn tất → hoàn kho
                                await RestoreStockForOrderAsync(order);
                                order.Status = "Đã hủy";
                                _context.Entry(order).State = EntityState.Modified;
                                await _context.SaveChangesAsync();

                                TempData["ErrorMessage"] = "Giao dịch thanh toán online không thành công hoặc chưa hoàn tất. Tồn kho đã được hoàn trả.";
                                return RedirectToAction(nameof(Index));
                            }
                        }
                        catch (Exception ex)
                        {
                            TempData["ErrorMessage"] = $"Lỗi khi xác minh thanh toán: {ex.Message}";
                            return RedirectToAction(nameof(Index));
                        }
                    }
                }

                if (model == null)
                {
                    model = new CheckoutSuccessViewModel
                    {
                        OrderId = order.OrderId,
                        FullName = customer.FullName,
                        Phone = customer.Phone,
                        ShippingAddress = "",
                        ConfirmationEmail = customer.Email ?? "",
                        PaymentMethod = order.PaymentMethod,
                        Total = order.Total,
                        ItemCount = order.OrderItems.Sum(i => i.Quantity)
                    };
                }

                if (order.PaymentMethod == "Thanh toán online" && order.Status == "Chờ xử lý")
                {
                    model.IsPaid = true;
                }
            }
        }

        if (model == null)
        {
            return RedirectToAction("Index", "Cart");
        }

        ViewBag.OrderNote = TempData["OrderNote"] ?? HttpContext.Session.GetString("OrderNote");
        ViewBag.EmailSentMessage = ViewBag.EmailSentMessage ?? TempData["EmailSentMessage"];
        ViewBag.EmailSentWarning = TempData["EmailSentWarning"];
        return View(model);
    }

    private async Task<ManagePetStore.Models.Customer?> GetCurrentCustomerAsync()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim == null)
        {
            return null;
        }

        if (!int.TryParse(userIdClaim.Value, out var userId))
        {
            return null;
        }

        return await _context.Customers.FirstOrDefaultAsync(c => c.UserId == userId);
    }

    /// <summary>
    /// Hoàn trả tồn kho cho tất cả sản phẩm trong đơn khi thanh toán thất bại/bị hủy.
    /// Đồng thời tạo phiếu "Nhập kho (Hủy đơn)" để ghi nhận lịch sử hoàn trả tồn kho.
    /// </summary>
    private async Task RestoreStockForOrderAsync(Order order)
    {
        // Load order items nếu chưa load
        if (!order.OrderItems.Any())
        {
            await _context.Entry(order).Collection(o => o.OrderItems).LoadAsync();
        }

        var restoredDetails = new List<StockMovementDetail>();

        foreach (var item in order.OrderItems)
        {
            if (string.IsNullOrEmpty(item.ProductSku)) continue; // Bỏ qua SPA service
            try
            {
                await _inventoryBatchService.RestockToBatches(item.ProductSku, item.Quantity);
                restoredDetails.Add(new StockMovementDetail
                {
                    ProductSku = item.ProductSku,
                    Quantity = item.Quantity,
                    CostPrice = item.Price
                });
            }
            catch
            {
                // Fallback: cộng trực tiếp vào Product.Stock nếu batch service lỗi
                await _context.Database.ExecuteSqlRawAsync(
                    "UPDATE Products SET Stock = Stock + {1} WHERE Sku = {0}",
                    item.ProductSku,
                    item.Quantity);
                restoredDetails.Add(new StockMovementDetail
                {
                    ProductSku = item.ProductSku,
                    Quantity = item.Quantity,
                    CostPrice = item.Price
                });
            }
        }

        // Tạo phiếu nhập hoàn trả để ghi nhận lịch sử rõ ràng trong trang Lịch sử Xuất/Nhập Kho.
        // Cùng pattern với luồng hủy thủ công trong Customer/OrderController.cs
        if (restoredDetails.Any())
        {
            await _stockMovementService.CreateSystemMovement(
                systemUserId: 1,
                type: "Nhập kho (Hủy đơn)",
                status: "Đã hoàn thành",
                supplier: $"Hoàn trả đơn {order.OrderId}",
                totalValue: restoredDetails.Sum(d => d.Quantity * d.CostPrice),
                details: restoredDetails
            );
        }
    }

    private static string? NormalizePaymentMethod(string paymentMethod)
    {
        return paymentMethod switch
        {
            "Cash" => "Tiền mặt",
            "PayOS" => "Thanh toán online",
            "Wallet" => "Ví điện tử",
            _ => null
        };
    }


    //Kiểm tra product tồn tại hay chưa
    private async Task EnsureProductForOrderItemAsync(CartLineItemViewModel item)
    {
        var exists = await _context.Database
            .SqlQueryRaw<int>("SELECT COUNT(1) AS [Value] FROM Products WHERE Sku = {0}", item.Sku)
            .FirstOrDefaultAsync() > 0;

        if (!exists)
        {
            var initialStock = Math.Max(0, item.MaxStock - item.Quantity);
            // Lấy CategoryId mặc định từ database
            var defaultCategory = await _context.ProductCategories.FirstOrDefaultAsync();
            int? categoryId = defaultCategory?.CategoryId;

            await _context.Database.ExecuteSqlRawAsync(
                """
                INSERT INTO Products (Sku, Name, CategoryId, Unit, Stock, MinStock, Price, ImageUrl)
                VALUES ({0}, {1}, {2}, {3}, {4}, 0, {5}, {6})
                """,
                item.Sku ?? string.Empty,
                item.Name ?? string.Empty,
                (object?)categoryId ?? DBNull.Value,
                "Cái",
                initialStock,
                item.UnitPrice,
                item.ImageUrl ?? string.Empty);
        }
        else
        {
            try 
            {
                if (!string.IsNullOrEmpty(item.Sku))
                {
                    await _inventoryBatchService.DeductStockFIFO(item.Sku, item.Quantity);
                }
            }
            catch (ManagePetStore.Exceptions.ServiceException)
            {
                // Fallback to basic deduction if batch service throws (e.g., stock mismatch)
                await _context.Database.ExecuteSqlRawAsync(
                    "UPDATE Products SET Stock = CASE WHEN Stock >= {1} THEN Stock - {1} ELSE 0 END WHERE Sku = {0}",
                    item.Sku,
                    item.Quantity);
            }
        }
    }
}
