// HÀ HOÀNG HIỆP CODE - LUỒNG VÍ ĐIỆN TỬ: USE CASE MANAGE WALLET (QUẢN LÝ VÍ ĐIỆN TỬ KHÁCH HÀNG)
using System.Security.Claims;
using ManagePetStore.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PayOS;
using PayOS.Models.V2.PaymentRequests;

namespace ManagePetStore.Areas.Customer.Controllers
{
    [Area("Customer")]
    public class WalletController : Controller
    {
        private readonly PetStoreManagementContext _context;
        private readonly PayOSClient _payOS;

        public WalletController(PetStoreManagementContext context, PayOSClient payOS)
        {
            _context = context;
            _payOS = payOS;
        }

        private async Task<int?> GetCurrentCustomerIdAsync()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null)
            {
                return null;
            }

            var userId = int.Parse(userIdClaim.Value);
            var customer = await _context.Customers.FirstOrDefaultAsync(c => c.UserId == userId);
            return customer?.CustomerId;
        }

        /// <summary>
        /// LUỒNG MANAGE WALLET: Xem thông tin và số dư ví điện tử
        /// - Chuyển hướng người dùng sang tab Wallet trong trang Profile tài khoản.
        /// </summary>
        public IActionResult Index()
        {
            return Redirect("/Customer/Account/Profile?tab=wallet");
        }

        /// <summary>
        /// LUỒNG MANAGE WALLET: Nạp tiền vào ví điện tử qua cổng PayOS
        /// - VALIDATION 1: Số tiền nạp tối thiểu là 10,000 VND (amount >= 10000).
        /// - Xử lý tạo link thanh toán PayOS và chuyển hướng người dùng đến trang nạp tiền QR Code.
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Deposit(decimal amount)
        {
            var customerId = await GetCurrentCustomerIdAsync();
            if (customerId == null)
            {
                return RedirectToAction("Login", "Account", new { area = "Customer" });
            }

            // RÀNG BUỘC SỐ TIỀN NẠP TỐI THIỂU
            if (amount < 10000)
            {
                TempData["Error"] = "Số tiền nạp tối thiểu là 10,000 VND.";
                return RedirectToAction("Index");
            }

            try
            {
                long orderCode = long.Parse(DateTimeOffset.Now.ToString("yyMMddHHmmss") + new Random().Next(10, 99).ToString());

                var domain = $"{Request.Scheme}://{Request.Host}";
                var returnUrl = $"{domain}/Customer/Wallet/DepositSuccess";
                var cancelUrl = $"{domain}/Customer/Wallet/DepositSuccess";

                var paymentData = new CreatePaymentLinkRequest
                {
                    OrderCode = orderCode,
                    Amount = (long)amount,
                    Description = "Nap tien vi dien tu",
                    Items = new List<PaymentLinkItem> { new PaymentLinkItem { Name = "Nạp tiền ví", Quantity = 1, Price = (long)amount } },
                    ReturnUrl = returnUrl,
                    CancelUrl = cancelUrl
                };

                var paymentLinkResult = await _payOS.PaymentRequests.CreateAsync(paymentData);
                
                // Lưu thông tin giao dịch tạm vào Session để đối soát sau khi nạp xong
                HttpContext.Session.SetString("DepositOrderCode", orderCode.ToString());
                HttpContext.Session.SetString("DepositAmount", amount.ToString());

                return Redirect(paymentLinkResult.CheckoutUrl);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Đã xảy ra lỗi khi tạo yêu cầu nạp tiền: " + ex.Message;
                return RedirectToAction("Index");
            }
        }

        /// <summary>
        /// LUỒNG MANAGE WALLET: Xử lý kết quả nạp tiền thành công từ cổng PayOS
        /// - Kiểm tra trạng thái giao dịch từ PayOS (PAID).
        /// - Cộng trực tiếp số tiền nạp vào số dư ví của khách hàng (Wallet.Balance += amount).
        /// - Ghi nhận lịch sử giao dịch nạp tiền (WalletTransaction: Type = "Deposit").
        /// </summary>
        public async Task<IActionResult> DepositSuccess([FromQuery] string? status, [FromQuery] string? cancel)
        {
            var customerId = await GetCurrentCustomerIdAsync();
            if (customerId == null) return RedirectToAction("Login", "Account", new { area = "Customer" });

            string? sessionOrderCode = HttpContext.Session.GetString("DepositOrderCode");
            string? sessionAmountStr = HttpContext.Session.GetString("DepositAmount");

            if (string.IsNullOrEmpty(sessionOrderCode) || string.IsNullOrEmpty(sessionAmountStr))
            {
                TempData["Error"] = "Không tìm thấy thông tin giao dịch nạp tiền.";
                return RedirectToAction("Index");
            }

            if (cancel == "true" || status == "CANCELLED")
            {
                TempData["Error"] = "Giao dịch nạp tiền đã bị hủy.";
                HttpContext.Session.Remove("DepositOrderCode");
                HttpContext.Session.Remove("DepositAmount");
                return RedirectToAction("Index");
            }

            try
            {
                long orderCode = long.Parse(sessionOrderCode);
                var paymentInfo = await _payOS.PaymentRequests.GetAsync(orderCode);

                if (paymentInfo != null && paymentInfo.Status.ToString().ToUpper() == "PAID")
                {
                    decimal amount = decimal.Parse(sessionAmountStr);

                    var wallet = await _context.Wallets.FirstOrDefaultAsync(w => w.CustomerId == customerId);
                    if (wallet != null)
                    {
                        // Cộng tiền số dư ví
                        wallet.Balance += amount;
                        wallet.UpdatedAt = DateTime.Now;

                        // Tạo nhật ký giao dịch ví
                        var transaction = new WalletTransaction
                        {
                            WalletId = wallet.WalletId,
                            Amount = amount,
                            Type = "Deposit",
                            Description = $"Nạp tiền trực tuyến qua PayOS (Mã GD: {orderCode})",
                            TransactionDate = DateTime.Now
                        };

                        _context.WalletTransactions.Add(transaction);
                        await _context.SaveChangesAsync();

                        TempData["Success"] = $"Nạp tiền thành công: {amount:N0} VND vào ví.";
                    }
                }
                else
                {
                    TempData["Error"] = "Giao dịch thanh toán không thành công.";
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Lỗi xác thực giao dịch nạp tiền: " + ex.Message;
            }

            HttpContext.Session.Remove("DepositOrderCode");
            HttpContext.Session.Remove("DepositAmount");
            return RedirectToAction("Index");
        }
    }
}
