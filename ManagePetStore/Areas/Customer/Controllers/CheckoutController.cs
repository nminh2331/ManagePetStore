

// hà hoàng hiệp code -- xử lý phần place order và make payment
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
    {//Ví dụ URL lúc này sẽ là: /Checkout?cancel=true&status=CANCELLED&orderCode=123456.
     ////Code sẽ bóc tách các giá trị true, CANCELLED và 123456 gán vào 3 biến tương ứng.
     ///
        string? payOsCancel = Request.Query["cancel"];
        string? payOsStatus = Request.Query["status"];
        string? orderCodeStr = Request.Query["orderCode"];

        // LOGIC HỦY ĐƠN HÀNG 
        if (payOsCancel == "true" || payOsStatus == "CANCELLED")
        {
            if (!string.IsNullOrEmpty(orderCodeStr) && long.TryParse(orderCodeStr, out long code))
            {
                var targetOrderId = $"ORD-{code}";  //Ghép nối chuỗi để tạo ra mã đơn hàng chuẩn trong hệ thống của bạn (Ví dụ hệ thống lưu mã là ORD-123456).

                //Chọc vào Database (_context.Orders), tìm đơn hàng đầu tiên (FirstOrDefaultAsync) có mã khớp với ORD-123456 hoặc chuẩn cũ là OD-123456.
                var orderToCancel = await _context.Orders.FirstOrDefaultAsync(o => o.OrderId == targetOrderId || o.OrderId == $"OD-{code}");

                if (orderToCancel != null && orderToCancel.Status == "Chờ thanh toán")
                {
                    orderToCancel.Status = "Đã hủy";  //Cập nhật trạng thái của đơn hàng đó thành "Đã hủy" trong bộ nhớ.
                   
                    _context.Entry(orderToCancel).State = EntityState.Modified; 
                    //Đánh dấu cho hệ thống biết rằng bản ghi đơn hàng này vừa bị chỉnh sửa, cần phải được lưu lại.
                 
                    await _context.SaveChangesAsync(); //Lưu sự thay đổi trạng thái này xuống Database vật lý một cách vĩnh viễn
                }
            }
            TempData["ErrorMessage"] = "Giao dịch thanh toán online đã bị hủy.";
            //Lưu một câu thông báo màu đỏ vào bộ nhớ tạm TempData để lát nữa hiển thị ra màn hình cho người dùng biết họ vừa hủy giao dịch
        }

        var cart = await _cartService.GetCartPageAsync(); // Gọi sang CartService để lấy toàn bộ dữ liệu giỏ hàng hiện tại của người dùng
                                                          // (bao gồm danh sách sản phẩm, số lượng, tổng tiền).
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


    /// <summary>
    /// LUỒNG PLACE ORDER & MAKE PAYMENT: Đặt hàng và Xử lý Thanh toán
    /// - VALIDATION 1: Kiểm tra Giỏ hàng không được rỗng.
    /// - VALIDATION 2: Kiểm tra thông tin Khách hàng đăng nhập.
    /// - VALIDATION 3: Validate định dạng Họ tên (chỉ chữ/khoảng trắng), SĐT (10 chữ số), Email đúng định dạng.
    /// - RÀNG BUỘC PHƯƠNG THỨC THANH TOÁN:
    ///   + "Tiền mặt" (COD): Trạng thái đơn "Chờ xử lý".
    ///   + "Ví điện tử": Kiểm tra số dư ví (Balance >= GrandTotal), trừ số dư và lưu lịch sử giao dịch ví. Trạng thái đơn "Chờ xử lý".
    ///   + "Thanh toán online" (PayOS): Trạng thái đơn "Chờ thanh toán", tạo link thanh toán QR Code PayOS.
    /// </summary>
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

        // 1. Kiểm tra giỏ hàng  --Lấy thông tin giỏ hàng từ CartService
        var cart = await _cartService.GetCartPageAsync();
        if (!cart.Items.Any())
        {
            TempData["ErrorMessage"] = "Giỏ hàng trống.";
            return RedirectToAction("Index", "Cart");
        }

        // 2. Kiểm tra thông tin tài khoản khách hàng
        var customer = await GetCurrentCustomerAsync();
        if (customer == null)
        {
            TempData["ErrorMessage"] = "Không tìm thấy thông tin khách hàng.";
            return RedirectToAction("Index", "Cart");
        }

        // 3. VALIDATION DỮ LIỆU ĐẦU VÀO: Bắt buộc nhập đầy đủ thông tin giao hàng - Bắt lỗi nhập thiếu 1 trong 4 trường bắt buộc
        if (string.IsNullOrWhiteSpace(trimmedFullName) ||
            string.IsNullOrWhiteSpace(trimmedPhone) ||
            string.IsNullOrWhiteSpace(trimmedEmail) ||
            string.IsNullOrWhiteSpace(trimmedShippingAddress))
        {
            TempData["ErrorMessage"] = "Vui lòng nhập đầy đủ họ tên, số điện thoại, Gmail và địa chỉ giao hàng.";
            return RedirectToAction(nameof(Index));
        }

        // Validate Regex Họ tên  - BE VALIDATE REGEX HỌ TÊN (Chỉ chứa chữ & khoảng trắng)
        var fullNameRegex = new Regex(@"^[\p{L}\s]+$");
        if (!fullNameRegex.IsMatch(trimmedFullName))
        {
            TempData["ErrorMessage"] = "Họ tên chỉ được chứa chữ và khoảng trắng, không được nhập số hoặc ký tự khác.";
            return RedirectToAction(nameof(Index));
        }

        // Validate Regex Số điện thoại (10 chữ số)
        var phoneRegex = new Regex(@"^\d{10}$");
        if (!phoneRegex.IsMatch(trimmedPhone))
        {
            TempData["ErrorMessage"] = "Số điện thoại phải là 10 chữ số và không được chứa ký tự đặc biệt.";
            return RedirectToAction(nameof(Index));
        }

        // Validate Regex Email
        var emailRegex = new Regex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$");
        if (!emailRegex.IsMatch(trimmedEmail))
        {
            TempData["ErrorMessage"] = "Gmail không đúng định dạng. Vui lòng kiểm tra lại.";
            return RedirectToAction(nameof(Index));
        }

        // 4. Validate và chuẩn hóa phương thức thanh toán
        var normalizedPayment = NormalizePaymentMethod(paymentMethod);
        if (normalizedPayment == null)
        {
            TempData["ErrorMessage"] = "Phương thức thanh toán không hợp lệ.";
            return RedirectToAction(nameof(Index));
        }

        // RÀNG BUỘC VÍ ĐIỆN TỬ: Kiểm tra số dư ví nếu chọn thanh toán qua Ví điện tử
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

        // 5. Tạo mã đơn hàng duy nhất (Mã số cho Online hoặc Mã chuỗi ngày giờ cho Tiền mặt/Ví)
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
            // Khởi tạo Transaction đảm bảo tính toàn vẹn dữ liệu (Atomic)
            await using var transaction = await _context.Database.BeginTransactionAsync();
            //Tạo entity Order
            var order = new Order
            {
                OrderId = orderId,
                CustomerId = customer.CustomerId,
                Subtotal = cart.Subtotal,
                Discount = 0,
                Total = cart.GrandTotal,
                PaymentMethod = normalizedPayment,
                PointsRedeemed = 0,
                PointsEarned = 10,
                Status = status,
                Date = DateTime.Now
            };

            _context.Orders.Add(order);  //Đưa entity vào dbCONTEXT change tracker, chưa save ngay.

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
                    bool isOnlinePayment = normalizedPayment == "Thanh toán online";
                    await EnsureProductForOrderItemAsync(item, deductStock: !isOnlinePayment);
                }
          
                
                //   Thêm chi tiết đơn hàng(OrderItems)
                _context.OrderItems.Add(new OrderItem
                {
                    OrderId = orderId,
                    ProductSku = isSpa ? null : item.Sku,
                    SpaServiceId = spaServiceIdVal,
                    Quantity = item.Quantity,
                    Price = item.UnitPrice,
                    IsCombo = false
                });

                if (!isSpa && !string.IsNullOrEmpty(item.Sku) && normalizedPayment != "Thanh toán online")
                {
                    systemStockDetails.Add(new StockMovementDetail
                    {
                        ProductSku = item.Sku,
                        Quantity = item.Quantity,
                        CostPrice = 0 // Not tracking cost for export right now
                    });
                }
            }


            // Gọi Cổng Thanh Toán PayOS & Gửi Email Hóa Đơn 
            string? payosCheckoutUrl = null;
            //NẾU CHỌN THANH TOÁN ONLINE -> GỌI SDK PAYOS TẠO VIETQR
            if (normalizedPayment == "Thanh toán online")
            {
                var host = $"{Request.Scheme}://{Request.Host}"; //Tự động lấy Domain của Website (Host URL)
               
                var paymentRequest = new CreatePaymentLinkRequest  //Đóng gói dữ liệu yêu cầu thanh toán
                {
                    OrderCode = orderCode,  // MÃ ĐƠN HÀNG
                    Amount = (long)cart.GrandTotal,  // TỔNG TIỀN 
                    Description = $"PetStore {orderCode}",  //Nội dung chuyển khoản ngân hàng
                   
                    // tự động đưa khách quay lại website của bạn nếu khách bấm Hủy thanh toán trên giao diện PayOS
                    CancelUrl = $"{host}/Customer/Checkout/Success?orderId={orderId}&cancel=true",
                  
                    //Đường dẫn PayOS sẽ đưa khách quay về sau khi khách đã quét mã thanh toán thành công.
                    ReturnUrl = $"{host}/Customer/Checkout/Success?orderId={orderId}",

                    //Sử dụng LINQ .Select(...) để duyệt danh sách sản phẩm trong giỏ hàng (cart.Items)
                    //và chuyển đổi thành danh sách chi tiết món hàng (PaymentLinkItem) mà PayOS yêu cầu để hiển thị trên hóa đơn điện tử của PayOS.
                    Items = cart.Items.Select(item => new PaymentLinkItem
                    {
                        Name = item.Name,
                        Quantity = item.Quantity,
                        Price = (long)item.UnitPrice
                    }).ToList()
                };

                //Gọi API PayOS tạo link thanh toán
                //Gửi toàn bộ dữ liệu vừa chuẩn bị sang hệ thống server của PayOS để tạo một link thanh toán VietQR động.
                var paymentLinkResult = await _payOS.PaymentRequests.CreateAsync(paymentRequest);
                payosCheckoutUrl = paymentLinkResult.CheckoutUrl; //Nhận kết quả trả về từ PayOS và lấy ra đường dẫn trang thanh toán
            }

            // ĐIỂM TÍCH LŨY SẼ ĐƯỢC TÍNH KHI ĐƠN HOÀN THÀNH 
            _context.Entry(customer).State = EntityState.Modified;


            //TRỪ TIỀN VÍ ĐIỆN TỬ (Nếu PHƯƠNG THỨC THANH TOÁN  = Ví điện tử)
            if (normalizedPayment == "Ví điện tử" && customerWallet != null)
            {
                customerWallet.Balance -= cart.GrandTotal;  // TRỪ SỐ DƯ BALANCE
                customerWallet.UpdatedAt = DateTime.Now;
               
                //Thủ công thông báo cho Entity Framework biết rằng thông tin ví tiền của khách hàng (customerWallet) đã bị thay đổi,
                //và chuẩn bị cập nhật (UPDATE) nó xuống CSDL.
                _context.Entry(customerWallet).State = EntityState.Modified;

                //    // Ghi nhật ký lịch sử giao dịch Ví (WalletTransaction)
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

            // TRỪ TỒN KHO SẢN PHẨM HỆ THỐNG (Gửi thông tin sang StockMovementService)
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
            {  //Xử lý xóa Giỏ hàng (Cho trường hợp KHÔNG phải thanh toán online)
                _cartService.ClearCart();
            }

            if (normalizedPayment == "Thanh toán online")
            {
                if (!string.IsNullOrWhiteSpace(orderNote))
                { //ếu có ghi chú đơn hàng (orderNote), tiến hành cắt khoảng trắng thừa (.Trim())
                  //và lưu vào cả TempData lẫn Session để giữ lại ghi chú này khi khách quay về từ PayOS
                    TempData["OrderNote"] = orderNote.Trim();
                    HttpContext.Session.SetString("OrderNote", orderNote.Trim());
                }
                //Chuyển danh sách sản phẩm trong giỏ (cart.Items) thành chuỗi văn bản JSON, rồi lưu vào Session kèm theo mã đơn hàng orderId.
                var cartItemsJson = System.Text.Json.JsonSerializer.Serialize(cart.Items.ToList());

                //rồi lưu vào Session kèm theo mã đơn hàng orderId
                //Mục đích: Khi khách thanh toán xong bên PayOS, hệ thống sẽ đọc Session này để biết đơn hàng gồm những món gì và gửi email hóa đơn cho khách.
                HttpContext.Session.SetString($"CheckoutCartItems_{orderId}", cartItemsJson);
                // Store success model so Success page can read it when coming from PayOS

                //Khởi tạo đối tượng payosSuccessModel chứa toàn bộ thông tin sẽ hiển thị trên trang "Đặt hàng thành công" (Mã đơn, Tên, SĐT, Địa chỉ, Email, Tổng tiền, Số lượng).
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
                //Mã hóa đối tượng này thành chuỗi JSON và cất vào Session tên là "CheckoutSuccess".
                //Vì khách sắp bị đẩy sang trang web khác (PayOS), nên cất vào Session là cách duy nhất để giữ lại thông tin này khi họ quay về.
                HttpContext.Session.SetString("CheckoutSuccess", System.Text.Json.JsonSerializer.Serialize(payosSuccessModel));

                if (!string.IsNullOrEmpty(payosCheckoutUrl))
                {
                    return Redirect(payosCheckoutUrl);
                }
            }


            //Chuẩn bị dữ liệu hiển thị (Trường hợp Thanh toán tiền mặt / COD)
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

            //Gửi email xác nhận VỀ GMAIL 
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
            //mã hóa (Serialize) đối tượng successModel (chứa các thông tin: Mã đơn, Tên, SĐT, Địa chỉ, Tổng tiền...) thành một chuỗi văn bản dạng JSON.
            var successJson = System.Text.Json.JsonSerializer.Serialize(successModel);

            //TempData["CheckoutSuccess"]: Lưu chuỗi JSON này vào TempData.
            //Mục đích là truyền dữ liệu sang trang Thông báo đặt hàng thành công (Success).
            TempData["CheckoutSuccess"] = successJson;
            HttpContext.Session.SetString("CheckoutSuccess", successJson);

            if (!string.IsNullOrWhiteSpace(orderNote))
            {
                TempData["OrderNote"] = orderNote.Trim();
            }
            //Gửi lệnh chuyển hướng trình duyệt (Redirect) sang trang Success (Action Method hiển thị kết quả đặt hàng thành công).
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
            //"Giải mã" chuỗi văn bản JSON thành đối tượng C# CheckoutSuccessViewModel và gán vào biến model để sẵn sàng gửi sang View.
            model = System.Text.Json.JsonSerializer.Deserialize<CheckoutSuccessViewModel>(json);
        }

        if (!string.IsNullOrEmpty(orderId))
        {
            var order = await _context.Orders
                .Include(o => o.OrderItems)  //Tải kèm danh sách các chi tiết món hàng thuộc đơn này (Eager Loading).
                .Include(o => o.Customer)  //Tải kèm thông tin Khách hàng sở hữu đơn hàng.
                .FirstOrDefaultAsync(o => o.OrderId == orderId);  //Tìm đơn hàng khớp chính xác với mã orderId.

            if (order != null)
            {
                var customer = order.Customer;

                if (order.PaymentMethod == "Thanh toán online" && order.Status == "Chờ thanh toán")
                {
                    var parts = orderId.Split('-');
                    if (parts.Length >= 2 && long.TryParse(parts[^1], out long orderCode))
                    {
                        try
                        { // Xử lý trường hợp Người dùng Hủy thanh toán
                            bool isPaid = false;
                            string? payOsStatus = Request.Query["status"];
                            string? payOsCancel = Request.Query["cancel"];

                            if (payOsCancel == "true" || payOsStatus == "CANCELLED")
                            {
                                // Không cần hoàn tồn kho vì lúc chốt đơn chưa trừ kho
                                order.Status = "Đã hủy";
                                _context.Entry(order).State = EntityState.Modified;
                                await _context.SaveChangesAsync();

                                TempData["ErrorMessage"] = "Giao dịch thanh toán đã bị hủy.";
                                return RedirectToAction(nameof(Index));
                            }

                            //Xác minh Trạng thái Thanh toán Thành công
                            if (payOsStatus == "PAID")
                            {
                                isPaid = true;
                            }
                            else
                            {  //chủ động gọi trực tiếp lệnh _payOS.PaymentRequests.GetAsync(orderCode) sang Server PayOS để vấn tin trạng thái giao dịch thực tế.
                                var paymentInfo = await _payOS.PaymentRequests.GetAsync(orderCode);
                                if (paymentInfo != null && paymentInfo.Status.ToString().ToUpper() == "PAID")
                                {
                                    isPaid = true;
                                }
                            }

                            if (isPaid)
                            {
                                // 1. Kiểm tra tồn kho trước khi trừ
                                bool outOfStock = false;
                                var systemStockDetails = new List<StockMovementDetail>();
                                foreach (var item in order.OrderItems)
                                {
                                    if (string.IsNullOrEmpty(item.ProductSku)) continue;
                                    var product = await _context.Products.FirstOrDefaultAsync(p => p.Sku == item.ProductSku);
                                    if (product == null || product.Stock < item.Quantity)
                                    {
                                        outOfStock = true;
                                        break;
                                    }
                                }

                                if (outOfStock)
                                {
                                    order.Status = "Chờ hoàn tiền";
                                    _context.Entry(order).State = EntityState.Modified;
                                    await _context.SaveChangesAsync();
                                    
                                    TempData["ErrorMessage"] = "Giao dịch của bạn đã thành công, nhưng sản phẩm đã hết hàng trong lúc bạn thanh toán. Vui lòng liên hệ Hotline cửa hàng để được hoàn tiền hoặc đổi sản phẩm khác.";
                                    _cartService.ClearCart();
                                    return RedirectToAction(nameof(Success), new { orderId = order.OrderId });
                                }
                                
                                // 2. Đủ hàng -> Tiến hành trừ lô và sinh phiếu Xuất kho
                                foreach (var item in order.OrderItems)
                                {
                                    if (string.IsNullOrEmpty(item.ProductSku)) continue;
                                    
                                    try 
                                    {
                                        await _inventoryBatchService.DeductStockFIFO(item.ProductSku, item.Quantity);
                                    }
                                    catch (ManagePetStore.Exceptions.ServiceException)
                                    {
                                        await _context.Products
                                            .Where(p => p.Sku == item.ProductSku)
                                            .ExecuteUpdateAsync(s => s.SetProperty(p => p.Stock, p => p.Stock >= item.Quantity ? p.Stock - item.Quantity : 0));
                                    }
                                    
                                    systemStockDetails.Add(new StockMovementDetail
                                    {
                                        ProductSku = item.ProductSku,
                                        Quantity = item.Quantity,
                                        CostPrice = 0
                                    });
                                }

                                if (systemStockDetails.Any())
                                {
                                    await _stockMovementService.CreateSystemMovement(
                                        systemUserId: 1, 
                                        type: "Xuất kho (Bán hàng online)",
                                        status: "Đã hoàn thành",
                                        supplier: null,
                                        totalValue: 0,
                                        details: systemStockDetails
                                    );
                                }

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
                                // Thanh toán không hoàn tất, không cần hoàn kho vì chưa trừ kho
                                order.Status = "Đã hủy";
                                _context.Entry(order).State = EntityState.Modified;
                                await _context.SaveChangesAsync();

                                TempData["ErrorMessage"] = "Giao dịch thanh toán online không thành công hoặc chưa hoàn tất.";
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

                if (order.PaymentMethod == "Thanh toán online" && (order.Status == "Chờ xử lý" || order.Status == "Chờ hoàn tiền"))
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
    private async Task EnsureProductForOrderItemAsync(CartLineItemViewModel item, bool deductStock = true)
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
        else if (deductStock)
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
                await _context.Products
                    .Where(p => p.Sku == item.Sku)
                    .ExecuteUpdateAsync(s => s.SetProperty(p => p.Stock, p => p.Stock >= item.Quantity ? p.Stock - item.Quantity : 0));
            }
        }
    }
}
