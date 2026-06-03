using System.Security.Claims;
using ManagePetStore.Areas.Customer.Models;
using ManagePetStore.Areas.Customer.Services;
using ManagePetStore.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ManagePetStore.Areas.Customer.Controllers;

[Area("Customer")]
[Authorize]
public class CheckoutController : Controller
{
    private readonly ICartService _cartService;
    private readonly PetStoreManagementContext _context;
    private readonly ICheckoutEmailService _checkoutEmailService;

    public CheckoutController(
        ICartService cartService,
        PetStoreManagementContext context,
        ICheckoutEmailService checkoutEmailService)
    {
        _cartService = cartService;
        _context = context;
        _checkoutEmailService = checkoutEmailService;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var cart = await _cartService.GetCartPageAsync();
        if (!cart.Items.Any())
        {
            TempData["ErrorMessage"] = "Giỏ hàng trống. Vui lòng thêm sản phẩm trước khi thanh toán.";
            return RedirectToAction("Index", "Cart");
        }

        var customer = await GetCurrentCustomerAsync();
        if (customer == null)
        {
            TempData["ErrorMessage"] = "Không tìm thấy thông tin khách hàng. Vui lòng đăng nhập tài khoản khách hàng.";
            return RedirectToAction("Index", "Cart");
        }

        var model = new CheckoutViewModel
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
        var cart = await _cartService.GetCartPageAsync();
        if (!cart.Items.Any())
        {
            TempData["ErrorMessage"] = "Giỏ hàng trống.";
            return RedirectToAction("Index", "Cart");
        }

        var customer = await GetCurrentCustomerAsync();
        if (customer == null)
        {
            TempData["ErrorMessage"] = "Không tìm thấy thông tin khách hàng.";
            return RedirectToAction("Index", "Cart");
        }

        if (string.IsNullOrWhiteSpace(fullName) ||
            string.IsNullOrWhiteSpace(phone) ||
            string.IsNullOrWhiteSpace(email) ||
            string.IsNullOrWhiteSpace(shippingAddress))
        {
            TempData["ErrorMessage"] = "Vui lòng nhập đầy đủ họ tên, số điện thoại, Gmail và địa chỉ giao hàng.";
            return RedirectToAction(nameof(Index));
        }

        var trimmedEmail = email.Trim();
        var emailRegex = new System.Text.RegularExpressions.Regex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$");
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

        var orderId = $"ORD-{DateTime.Now:yyyyMMddHHmmss}-{Random.Shared.Next(1000, 9999)}";
        var status = normalizedPayment == "Tiền mặt" ? "Chờ xử lý" : "Chờ thanh toán";

        try
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();

            var order = new Order
            {
                OrderId = orderId,
                CustomerId = customer.CustomerId,
                Subtotal = cart.Subtotal,
                Discount = cart.VoucherDiscount,
                Total = cart.GrandTotal,
                PaymentMethod = normalizedPayment,
                PointsRedeemed = 0,
                PointsEarned = (int)Math.Floor(cart.GrandTotal / 10000m),
                Status = status,
                Date = DateTime.Now
            };

            _context.Orders.Add(order);

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
                            pet = new Pet
                            {
                                CustomerId = customer.CustomerId,
                                Name = "Pet của " + customer.FullName,
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
                            var defaultGroomer = await _context.Users.FirstOrDefaultAsync(u => u.RoleId == 3 && u.Status == "Active")
                                                ?? await _context.Users.FirstOrDefaultAsync(u => u.RoleId == 3)
                                                ?? await _context.Users.FirstOrDefaultAsync();
                            int groomerId = defaultGroomer?.UserId ?? 3;

                            var bookingTime = DateTime.Today.AddHours(9);
                            if (DateTime.Now >= bookingTime)
                            {
                                bookingTime = DateTime.Today.AddDays(1).AddHours(9);
                            }

                            var bookingStatus = (normalizedPayment == "Tiền mặt") ? "Chưa thanh toán" : "Đã thanh toán";

                            var spaBooking = new SpaBooking
                            {
                                PetId = petId,
                                CustomerId = customer.CustomerId,
                                ServiceId = serviceId,
                                DateTime = bookingTime,
                                GroomerId = groomerId,
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

                _context.OrderItems.Add(new OrderItem
                {
                    OrderId = orderId,
                    ProductSku = isSpa ? null : item.Sku,
                    SpaServiceId = spaServiceIdVal,
                    Quantity = item.Quantity,
                    Price = item.UnitPrice,
                    IsCombo = false
                });
            }

            customer.LoyaltyPoints += (int)Math.Floor(cart.GrandTotal / 10000m);
            _context.Entry(customer).State = EntityState.Modified;

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            _cartService.ClearCart();

            var successModel = new CheckoutSuccessViewModel
            {
                OrderId = orderId,
                FullName = fullName.Trim(),
                Phone = phone.Trim(),
                ShippingAddress = shippingAddress.Trim(),
                ConfirmationEmail = trimmedEmail,
                PaymentMethod = normalizedPayment,
                Total = cart.GrandTotal,
                ItemCount = cart.TotalQuantity
            };

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

            TempData["CheckoutSuccess"] = System.Text.Json.JsonSerializer.Serialize(successModel);

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
    public IActionResult Success()
    {
        var json = TempData["CheckoutSuccess"] as string;
        if (string.IsNullOrEmpty(json))
        {
            return RedirectToAction("Index", "Cart");
        }

        var model = System.Text.Json.JsonSerializer.Deserialize<CheckoutSuccessViewModel>(json);
        if (model == null)
        {
            return RedirectToAction("Index", "Cart");
        }

        ViewBag.OrderNote = TempData["OrderNote"];
        ViewBag.EmailSentMessage = TempData["EmailSentMessage"];
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
            "VNPay" => "VNPay",
            "MoMo" => "MoMo",
            _ => null
        };
    }

    private async Task EnsureProductForOrderItemAsync(CartLineItemViewModel item)
    {
        var exists = await _context.Database
            .SqlQueryRaw<int>("SELECT COUNT(1) AS [Value] FROM Products WHERE Sku = {0}", item.Sku)
            .FirstOrDefaultAsync() > 0;

        if (!exists)
        {
            var initialStock = Math.Max(0, item.MaxStock - item.Quantity);
            await _context.Database.ExecuteSqlRawAsync(
                """
                INSERT INTO Products (Sku, Name, Category, Unit, Stock, MinStock, Price, ImageUrl)
                VALUES ({0}, {1}, {2}, {3}, {4}, 0, {5}, {6})
                """,
                item.Sku,
                item.Name,
                "Online",
                "Cái",
                initialStock,
                item.UnitPrice,
                item.ImageUrl ?? string.Empty);
        }
        else
        {
            await _context.Database.ExecuteSqlRawAsync(
                "UPDATE Products SET Stock = CASE WHEN Stock >= {1} THEN Stock - {1} ELSE 0 END WHERE Sku = {0}",
                item.Sku,
                item.Quantity);
        }
    }
}
