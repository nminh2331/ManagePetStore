using System.Security.Claims;
using System.Text.Json;
using ManagePetStore.Areas.Customer.Models;
using ManagePetStore.Models;
using ManagePetStore.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ManagePetStore.Areas.Customer.Controllers
{
    [Area("Customer")]
    public class AccountController : Controller
    {
        private const string PendingRegistrationSessionKey = "PendingRegistration";
        private const string PendingPasswordResetSessionKey = "PendingPasswordReset";
        private const int OtpExpiryMinutes = 10;

        private readonly PetStoreManagementContext _context;
        private readonly IEmailService _emailService;

        public AccountController(PetStoreManagementContext context, IEmailService emailService)
        {
            _context = context;
            _emailService = emailService;
        }

        // =========================================================================
        // 1. ĐĂNG NHẬP (LOGIN)
        // =========================================================================
        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                return RedirectToDashboard();
            }
            ViewData["ReturnUrl"] = returnUrl;

            if (returnUrl != null && returnUrl.Contains("SPA-SVC", StringComparison.OrdinalIgnoreCase))
            {
                TempData["ErrorMessage"] = "Bạn phải đăng nhập tài khoản mới có thể đặt lịch dịch vụ.";
            }

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(string usernameOrEmail, string password, bool rememberMe, string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;

            if (string.IsNullOrEmpty(usernameOrEmail) || string.IsNullOrEmpty(password))
            {
                ModelState.AddModelError("", "Vui lòng nhập đầy đủ email và mật khẩu.");
                return View();
            }

            // Tìm user bằng email (email làm tên đăng nhập thay thế)
            var user = await _context.Users
                .Include(u => u.Role)
                .Include(u => u.Customer)
                .FirstOrDefaultAsync(u => u.Email == usernameOrEmail);

            if (user == null || user.Password != password) // Plain-text password check
            {
                ModelState.AddModelError("", "Email hoặc mật khẩu không chính xác.");
                return View();
            }

            if (user.Status != "Active")
            {
                ModelState.AddModelError("", "Tài khoản của bạn đã bị khóa hoặc ngưng hoạt động.");
                return View();
            }

            // Chặn nhân viên đăng nhập ở trang khách hàng
            if (user.RoleId != 5)
            {
                ModelState.AddModelError("", "Tài khoản nhân viên không được phép đăng nhập tại trang dành cho khách hàng.");
                return View();
            }

            // Cấu hình Cookie Claims
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, user.Email ?? ""),
                new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
                new Claim(ClaimTypes.Role, user.Role.RoleName),
                new Claim("FullName", user.FullName),
                new Claim("RoleId", user.RoleId.ToString())
            };

            if (!string.IsNullOrEmpty(user.Email))
            {
                claims.Add(new Claim(ClaimTypes.Email, user.Email));
            }

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var authProperties = new AuthenticationProperties
            {
                IsPersistent = rememberMe,
                ExpiresUtc = DateTimeOffset.UtcNow.AddDays(7)
            };

            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(claimsIdentity), authProperties);

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return RedirectToDashboard(user.Role.RoleName);
        }

        // =========================================================================
        // 2. ĐĂNG KÝ (REGISTER)
        // =========================================================================
        [HttpGet]
        public IActionResult Register()
        {
            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                return RedirectToDashboard();
            }
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(string fullName, string email, string phone, string password, string confirmPassword)
        {
            // Validate fullName
            if (string.IsNullOrWhiteSpace(fullName))
            {
                ModelState.AddModelError("fullName", "Họ và tên không được để trống.");
            }
            else if (fullName.Any(char.IsDigit))
            {
                ModelState.AddModelError("fullName", "Họ và tên không được chứa chữ số.");
            }

            // Validate email
            if (string.IsNullOrWhiteSpace(email))
            {
                ModelState.AddModelError("email", "Email không được để trống.");
            }
            else
            {
                var emailRegex = new System.Text.RegularExpressions.Regex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$");
                if (!emailRegex.IsMatch(email))
                {
                    ModelState.AddModelError("email", "Email không đúng định dạng.");
                }
                else if (await _context.Users.AnyAsync(u => u.Email == email) || await _context.Customers.AnyAsync(c => c.Email == email))
                {
                    ModelState.AddModelError("email", "Email đã được sử dụng bởi tài khoản khác.");
                }
            }

            // Validate phone
            if (string.IsNullOrWhiteSpace(phone))
            {
                ModelState.AddModelError("phone", "Số điện thoại không được để trống.");
            }
            else
            {
                if (!ValidatePhone(phone, out var phoneErr))
                {
                    ModelState.AddModelError("phone", phoneErr);
                }
                else if (await _context.Users.AnyAsync(u => u.Phone == phone) || await _context.Customers.AnyAsync(c => c.Phone == phone))
                {
                    ModelState.AddModelError("phone", "Số điện thoại đã được sử dụng bởi tài khoản khác.");
                }
            }

            // Validate password
            if (string.IsNullOrEmpty(password))
            {
                ModelState.AddModelError("password", "Mật khẩu không được để trống.");
            }
            else if (!ValidatePasswordStrength(password, out var passErr))
            {
                ModelState.AddModelError("password", passErr);
            }

            // Validate confirm password
            if (string.IsNullOrEmpty(confirmPassword))
            {
                ModelState.AddModelError("confirmPassword", "Xác nhận mật khẩu không được để trống.");
            }
            else if (password != confirmPassword)
            {
                ModelState.AddModelError("confirmPassword", "Mật khẩu và xác nhận mật khẩu không khớp.");
            }

            if (!ModelState.IsValid)
            {
                ViewBag.FullName = (ModelState["fullName"]?.Errors == null || ModelState["fullName"]?.Errors.Count == 0) ? fullName : "";
                ViewBag.Email = (ModelState["email"]?.Errors == null || ModelState["email"]?.Errors.Count == 0) ? email : "";
                ViewBag.Phone = (ModelState["phone"]?.Errors == null || ModelState["phone"]?.Errors.Count == 0) ? phone : "";
                return View();
            }

            try
            {
                var otpCode = GenerateOtpCode();
                var pending = new PendingRegistration
                {
                    FullName = fullName.Trim(),
                    Email = email.Trim(),
                    Phone = phone.Trim(),
                    Password = password,
                    OtpCode = otpCode,
                    ExpiresAt = DateTime.UtcNow.AddMinutes(OtpExpiryMinutes)
                };

                SavePendingRegistration(pending);
                await SendOtpEmailAsync(pending.Email, pending.FullName, otpCode);

                TempData["OtpSentMessage"] = $"Mã OTP đã được gửi đến {pending.Email}. Mã có hiệu lực trong {OtpExpiryMinutes} phút.";
                return RedirectToAction(nameof(VerifyOtp));
            }
            catch (Exception ex) when (ex is Microsoft.Data.SqlClient.SqlException or InvalidOperationException)
            {
                ModelState.AddModelError("", "Không thể kết nối cơ sở dữ liệu. Vui lòng kiểm tra SQL Server và thử lại sau.");
                ViewBag.FullName = fullName;
                ViewBag.Email = email;
                ViewBag.Phone = phone;
                return View();
            }
            catch (Exception)
            {
                ModelState.AddModelError("", "Không thể gửi email OTP. Vui lòng kiểm tra cấu hình Gmail trong appsettings.json và thử lại.");
                ViewBag.FullName = fullName;
                ViewBag.Email = email;
                ViewBag.Phone = phone;
                return View();
            }
        }

        [HttpGet]
        public IActionResult VerifyOtp()
        {
            var pending = GetPendingRegistration();
            if (pending == null)
            {
                return RedirectToAction(nameof(Register));
            }

            return View(new VerifyOtpViewModel { Email = pending.Email });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VerifyOtp(string otpCode)
        {
            var pending = GetPendingRegistration();
            if (pending == null)
            {
                ModelState.AddModelError("", "Phiên đăng ký đã hết hạn. Vui lòng đăng ký lại.");
                return RedirectToAction(nameof(Register));
            }

            if (string.IsNullOrWhiteSpace(otpCode))
            {
                ModelState.AddModelError("", "Vui lòng nhập mã OTP.");
                return View(new VerifyOtpViewModel { Email = pending.Email });
            }

            if (DateTime.UtcNow > pending.ExpiresAt)
            {
                HttpContext.Session.Remove(PendingRegistrationSessionKey);
                TempData["ErrorMessage"] = "Mã OTP đã hết hạn. Vui lòng đăng ký lại.";
                return RedirectToAction(nameof(Register));
            }

            if (!string.Equals(pending.OtpCode, otpCode.Trim(), StringComparison.Ordinal))
            {
                ModelState.AddModelError("", "Mã OTP không chính xác. Vui lòng kiểm tra lại email.");
                return View(new VerifyOtpViewModel { Email = pending.Email, OtpCode = otpCode });
            }

            try
            {
                if (await _context.Users.AnyAsync(u => u.Email == pending.Email))
                {
                    HttpContext.Session.Remove(PendingRegistrationSessionKey);
                    TempData["ErrorMessage"] = "Email đã được sử dụng. Vui lòng đăng ký lại.";
                    return RedirectToAction(nameof(Register));
                }

                await using var transaction = await _context.Database.BeginTransactionAsync();

                    var newUser = new User
                    {
                    Password = pending.Password,
                    FullName = pending.FullName,
                    Email = pending.Email,
                    Phone = pending.Phone,
                    RoleId = 5,
                        Status = "Active",
                        CreatedAt = DateTime.Now
                    };

                    _context.Users.Add(newUser);
                await _context.SaveChangesAsync();

                    var newCustomer = new ManagePetStore.Models.Customer
                    {
                        UserId = newUser.UserId,
                    FullName = pending.FullName,
                    Phone = pending.Phone,
                    Email = pending.Email,
                        LoyaltyPoints = 0,
                        MembershipTier = "Bronze",
                        CreatedAt = DateTime.Now
                    };

                    _context.Customers.Add(newCustomer);
                    await _context.SaveChangesAsync();

                    await transaction.CommitAsync();
                HttpContext.Session.Remove(PendingRegistrationSessionKey);

                    TempData["SuccessMessage"] = "Đăng ký tài khoản thành công! Vui lòng đăng nhập.";
                return RedirectToAction(nameof(Login));
            }
            catch (Exception ex) when (ex is Microsoft.Data.SqlClient.SqlException or InvalidOperationException)
            {
                ModelState.AddModelError("", "Không thể kết nối cơ sở dữ liệu. Vui lòng thử lại sau.");
                return View(new VerifyOtpViewModel { Email = pending.Email });
            }
            catch (Exception)
            {
                ModelState.AddModelError("", "Có lỗi xảy ra khi tạo tài khoản. Vui lòng thử lại.");
                return View(new VerifyOtpViewModel { Email = pending.Email });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResendOtp()
        {
            var pending = GetPendingRegistration();
            if (pending == null)
            {
                TempData["ErrorMessage"] = "Phiên đăng ký đã hết hạn. Vui lòng đăng ký lại.";
                return RedirectToAction(nameof(Register));
            }

            try
            {
                pending.OtpCode = GenerateOtpCode();
                pending.ExpiresAt = DateTime.UtcNow.AddMinutes(OtpExpiryMinutes);
                SavePendingRegistration(pending);
                await SendOtpEmailAsync(pending.Email, pending.FullName, pending.OtpCode);

                TempData["OtpSentMessage"] = "Mã OTP mới đã được gửi đến email của bạn.";
                return RedirectToAction(nameof(VerifyOtp));
            }
            catch (Exception)
            {
                TempData["ErrorMessage"] = "Không thể gửi lại mã OTP. Vui lòng thử lại sau.";
                return RedirectToAction(nameof(VerifyOtp));
            }
        }

        // Removed ValidateRegistrationInput as validation is now inlined in the POST Register action.

        private bool ValidatePasswordStrength(string password, out string errorMessage)
        {
            errorMessage = "";
            if (string.IsNullOrEmpty(password))
            {
                errorMessage = "Mật khẩu không được để trống.";
                return false;
            }
            if (password.Length < 8 || password.Length > 25)
            {
                errorMessage = "Mật khẩu phải từ 8 đến 25 ký tự.";
                return false;
            }
            if (!password.Any(char.IsUpper))
            {
                errorMessage = "Mật khẩu phải chứa ít nhất 1 chữ cái in hoa.";
                return false;
            }
            if (!password.Any(char.IsDigit))
            {
                errorMessage = "Mật khẩu phải chứa ít nhất 1 chữ số.";
                return false;
            }
            string specialCh = @"%!@#$%^&*()_+{}|[]\:";
            bool hasSpecial = password.Any(c => specialCh.Contains(c) || char.IsSymbol(c) || char.IsPunctuation(c));
            if (!hasSpecial)
            {
                errorMessage = "Mật khẩu phải chứa ít nhất 1 ký tự đặc biệt.";
                return false;
            }
            // Check consecutive repeating (e.g. aaa)
            for (int i = 0; i < password.Length - 2; i++)
            {
                if (password[i] == password[i + 1] && password[i] == password[i + 2])
                {
                    errorMessage = "Mật khẩu không được chứa ký tự lặp lại liên tiếp (ví dụ: aaa).";
                    return false;
                }
            }
            // Check consecutive sequential (e.g. abc, 123)
            for (int i = 0; i < password.Length - 2; i++)
            {
                char c1 = password[i];
                char c2 = password[i + 1];
                char c3 = password[i + 2];
                if (c2 == c1 + 1 && c3 == c2 + 1)
                {
                    errorMessage = "Mật khẩu không được chứa chuỗi ký tự liên tiếp tăng dần (ví dụ: 123, abc).";
                    return false;
                }
                if (c2 == c1 - 1 && c3 == c2 - 1)
                {
                    errorMessage = "Mật khẩu không được chứa chuỗi ký tự liên tiếp giảm dần (ví dụ: 321, cba).";
                    return false;
                }
            }
            return true;
        }

        private bool ValidatePhone(string phone, out string errorMessage)
        {
            errorMessage = "";
            if (string.IsNullOrEmpty(phone) || phone.Length != 10 || !phone.StartsWith("0") || !phone.All(char.IsDigit))
            {
                errorMessage = "Số điện thoại phải bắt đầu bằng số 0 và có đúng 10 chữ số.";
                return false;
            }
            return true;
        }

        private static bool ValidateProfilePhone(string phone, out string errorMessage)
        {
            errorMessage = "";

            if (string.IsNullOrWhiteSpace(phone))
            {
                errorMessage = "Số điện thoại không được để trống.";
                return false;
            }

            var digitsOnly = phone.Trim();
            if (!digitsOnly.All(char.IsDigit))
            {
                errorMessage = "Số điện thoại chỉ được chứa chữ số, không được có chữ cái.";
                return false;
            }

            if (digitsOnly.Length < 10)
            {
                errorMessage = "Số điện thoại phải có ít nhất 10 chữ số.";
                return false;
            }

            if (!long.TryParse(digitsOnly, out var numericValue) || numericValue <= 0)
            {
                errorMessage = "Số điện thoại phải là số lớn hơn 0.";
                return false;
            }

            return true;
        }

        private static string GenerateOtpCode()
        {
            return Random.Shared.Next(100000, 999999).ToString();
        }

        private void SavePendingRegistration(PendingRegistration pending)
        {
            var json = JsonSerializer.Serialize(pending);
            HttpContext.Session.SetString(PendingRegistrationSessionKey, json);
        }

        private PendingRegistration? GetPendingRegistration()
        {
            var json = HttpContext.Session.GetString(PendingRegistrationSessionKey);
            if (string.IsNullOrEmpty(json))
            {
                return null;
            }

            return JsonSerializer.Deserialize<PendingRegistration>(json);
        }

        private async Task SendOtpEmailAsync(string email, string fullName, string otpCode)
        {
            var subject = "Mã xác thực đăng ký PetStore";
            var body = $@"
                <div style=""font-family: Arial, sans-serif; max-width: 520px; margin: 0 auto;"">
                    <h2 style=""color: #f97316;"">PetStore - Xác thực đăng ký</h2>
                    <p>Xin chào <strong>{fullName}</strong>,</p>
                    <p>Mã OTP của bạn là:</p>
                    <p style=""font-size: 32px; font-weight: bold; letter-spacing: 8px; color: #3d2314;"">{otpCode}</p>
                    <p>Mã có hiệu lực trong <strong>{OtpExpiryMinutes} phút</strong>. Không chia sẻ mã này với bất kỳ ai.</p>
                    <p style=""color: #6b7280; font-size: 13px;"">Nếu bạn không yêu cầu đăng ký, hãy bỏ qua email này.</p>
                </div>";

            await _emailService.SendEmailAsync(email, subject, body);
        }

        // =========================================================================
        // 3. QUÊN MẬT KHẨU (FORGOT PASSWORD)
        // =========================================================================
        [HttpGet]
        public IActionResult ForgotPassword()
        {
            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                return RedirectToDashboard();
            }

            return View(new ForgotPasswordViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForgotPassword(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                ModelState.AddModelError("", "Vui lòng nhập email đăng ký.");
                return View(new ForgotPasswordViewModel());
            }

            var trimmedEmail = email.Trim();
            var emailRegex = new System.Text.RegularExpressions.Regex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$");
            if (!emailRegex.IsMatch(trimmedEmail))
            {
                ModelState.AddModelError("", "Email không đúng định dạng.");
                return View(new ForgotPasswordViewModel { Email = trimmedEmail });
            }

            try
            {
                var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == trimmedEmail);
                if (user == null)
                {
                    ModelState.AddModelError("", "Email không tồn tại trong hệ thống.");
                    return View(new ForgotPasswordViewModel { Email = trimmedEmail });
                }

                if (user.Status != "Active")
                {
                    ModelState.AddModelError("", "Tài khoản của bạn đã bị khóa hoặc ngưng hoạt động.");
                    return View(new ForgotPasswordViewModel { Email = trimmedEmail });
                }

                var otpCode = GenerateOtpCode();
                var pending = new PendingPasswordReset
                {
                    Email = trimmedEmail,
                    UserId = user.UserId,
                    FullName = user.FullName,
                    OtpCode = otpCode,
                    ExpiresAt = DateTime.UtcNow.AddMinutes(OtpExpiryMinutes),
                    OtpVerified = false
                };

                SavePendingPasswordReset(pending);
                await SendForgotPasswordOtpEmailAsync(pending.Email, pending.FullName, otpCode);

                TempData["OtpSentMessage"] = $"Mã OTP đã được gửi đến {pending.Email}. Mã có hiệu lực trong {OtpExpiryMinutes} phút.";
                return RedirectToAction(nameof(ForgotPasswordVerifyOtp));
            }
            catch (Exception ex) when (ex is Microsoft.Data.SqlClient.SqlException or InvalidOperationException)
            {
                ModelState.AddModelError("", "Không thể kết nối cơ sở dữ liệu. Vui lòng thử lại sau.");
                return View(new ForgotPasswordViewModel { Email = trimmedEmail });
            }
            catch (Exception)
            {
                ModelState.AddModelError("", "Không thể gửi email OTP. Vui lòng kiểm tra cấu hình Gmail trong appsettings.json và thử lại.");
                return View(new ForgotPasswordViewModel { Email = trimmedEmail });
            }
        }

        [HttpGet]
        public IActionResult ForgotPasswordVerifyOtp()
        {
            var pending = GetPendingPasswordReset();
            if (pending == null)
            {
                return RedirectToAction(nameof(ForgotPassword));
            }

            return View(new ForgotPasswordVerifyOtpViewModel { Email = pending.Email });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ForgotPasswordVerifyOtp(string otpCode)
        {
            var pending = GetPendingPasswordReset();
            if (pending == null)
            {
                TempData["ErrorMessage"] = "Phiên đặt lại mật khẩu đã hết hạn. Vui lòng thử lại.";
                return RedirectToAction(nameof(ForgotPassword));
            }

            if (string.IsNullOrWhiteSpace(otpCode))
            {
                ModelState.AddModelError("", "Vui lòng nhập mã OTP.");
                return View(new ForgotPasswordVerifyOtpViewModel { Email = pending.Email });
            }

            if (DateTime.UtcNow > pending.ExpiresAt)
            {
                HttpContext.Session.Remove(PendingPasswordResetSessionKey);
                TempData["ErrorMessage"] = "Mã OTP đã hết hạn. Vui lòng yêu cầu mã mới.";
                return RedirectToAction(nameof(ForgotPassword));
            }

            if (!string.Equals(pending.OtpCode, otpCode.Trim(), StringComparison.Ordinal))
            {
                ModelState.AddModelError("", "Mã OTP không chính xác. Vui lòng kiểm tra lại email.");
                return View(new ForgotPasswordVerifyOtpViewModel { Email = pending.Email, OtpCode = otpCode });
            }

            pending.OtpVerified = true;
            SavePendingPasswordReset(pending);
            return RedirectToAction(nameof(ResetPassword));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResendForgotPasswordOtp()
        {
            var pending = GetPendingPasswordReset();
            if (pending == null)
            {
                TempData["ErrorMessage"] = "Phiên đặt lại mật khẩu đã hết hạn. Vui lòng thử lại.";
                return RedirectToAction(nameof(ForgotPassword));
            }

            try
            {
                pending.OtpCode = GenerateOtpCode();
                pending.ExpiresAt = DateTime.UtcNow.AddMinutes(OtpExpiryMinutes);
                pending.OtpVerified = false;
                SavePendingPasswordReset(pending);
                await SendForgotPasswordOtpEmailAsync(pending.Email, pending.FullName, pending.OtpCode);

                TempData["OtpSentMessage"] = "Mã OTP mới đã được gửi đến email của bạn.";
                return RedirectToAction(nameof(ForgotPasswordVerifyOtp));
            }
            catch (Exception)
            {
                TempData["ErrorMessage"] = "Không thể gửi lại mã OTP. Vui lòng thử lại sau.";
                return RedirectToAction(nameof(ForgotPasswordVerifyOtp));
            }
        }

        [HttpGet]
        public IActionResult ResetPassword()
        {
            var pending = GetPendingPasswordReset();
            if (pending == null || !pending.OtpVerified)
            {
                return RedirectToAction(nameof(ForgotPassword));
            }

            return View(new ResetPasswordViewModel { Email = pending.Email });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(string newPassword, string confirmNewPassword)
        {
            var pending = GetPendingPasswordReset();
            if (pending == null || !pending.OtpVerified)
            {
                TempData["ErrorMessage"] = "Phiên đặt lại mật khẩu đã hết hạn. Vui lòng thử lại.";
                return RedirectToAction(nameof(ForgotPassword));
            }

            if (string.IsNullOrEmpty(newPassword) || string.IsNullOrEmpty(confirmNewPassword))
            {
                ModelState.AddModelError("", "Vui lòng nhập đầy đủ mật khẩu mới và xác nhận mật khẩu.");
                return View(new ResetPasswordViewModel { Email = pending.Email });
            }

            if (newPassword != confirmNewPassword)
            {
                ModelState.AddModelError("", "Mật khẩu mới và xác nhận mật khẩu không khớp.");
                return View(new ResetPasswordViewModel { Email = pending.Email });
            }

            if (!ValidatePasswordStrength(newPassword, out var passErr))
            {
                ModelState.AddModelError("", passErr);
                return View(new ResetPasswordViewModel { Email = pending.Email });
            }

            try
            {
                var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == pending.UserId);
                if (user == null)
                {
                    HttpContext.Session.Remove(PendingPasswordResetSessionKey);
                    TempData["ErrorMessage"] = "Không tìm thấy tài khoản. Vui lòng thử lại.";
                    return RedirectToAction(nameof(ForgotPassword));
                }

                user.Password = newPassword;
                _context.Entry(user).State = EntityState.Modified;
                await _context.SaveChangesAsync();

                HttpContext.Session.Remove(PendingPasswordResetSessionKey);
                TempData["SuccessMessage"] = "Đã đổi mật khẩu thành công! Vui lòng đăng nhập bằng mật khẩu mới.";
                return RedirectToAction(nameof(Login));
            }
            catch (Exception ex) when (ex is Microsoft.Data.SqlClient.SqlException or InvalidOperationException)
            {
                ModelState.AddModelError("", "Không thể kết nối cơ sở dữ liệu. Vui lòng thử lại sau.");
                return View(new ResetPasswordViewModel { Email = pending.Email });
            }
            catch (Exception)
            {
                ModelState.AddModelError("", "Có lỗi xảy ra khi đổi mật khẩu. Vui lòng thử lại.");
                return View(new ResetPasswordViewModel { Email = pending.Email });
            }
        }

        private void SavePendingPasswordReset(PendingPasswordReset pending)
        {
            var json = JsonSerializer.Serialize(pending);
            HttpContext.Session.SetString(PendingPasswordResetSessionKey, json);
        }

        private PendingPasswordReset? GetPendingPasswordReset()
        {
            var json = HttpContext.Session.GetString(PendingPasswordResetSessionKey);
            if (string.IsNullOrEmpty(json))
            {
                return null;
            }

            return JsonSerializer.Deserialize<PendingPasswordReset>(json);
        }

        private async Task SendForgotPasswordOtpEmailAsync(string email, string fullName, string otpCode)
        {
            var subject = "Mã xác thực đặt lại mật khẩu PetStore";
            var body = $@"
                <div style=""font-family: Arial, sans-serif; max-width: 520px; margin: 0 auto;"">
                    <h2 style=""color: #f97316;"">PetStore - Đặt lại mật khẩu</h2>
                    <p>Xin chào <strong>{fullName}</strong>,</p>
                    <p>Bạn đã yêu cầu đặt lại mật khẩu. Mã OTP của bạn là:</p>
                    <p style=""font-size: 32px; font-weight: bold; letter-spacing: 8px; color: #3d2314;"">{otpCode}</p>
                    <p>Mã có hiệu lực trong <strong>{OtpExpiryMinutes} phút</strong>. Không chia sẻ mã này với bất kỳ ai.</p>
                    <p style=""color: #6b7280; font-size: 13px;"">Nếu bạn không yêu cầu đặt lại mật khẩu, hãy bỏ qua email này.</p>
                </div>";

            await _emailService.SendEmailAsync(email, subject, body);
        }

        // =========================================================================
        // 4. ĐĂNG XUẤT (LOGOUT)
        // =========================================================================
        [HttpPost]
        [HttpGet]
        public async Task<IActionResult> Logout()
        {
            bool isStaff = User.Identity != null && User.Identity.IsAuthenticated && !User.IsInRole("customer");
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            if (isStaff)
            {
                return Redirect("/Staff/Login");
            }
            return RedirectToAction("Index", "Home", new { area = "" });
        }

        // =========================================================================
        // 5. QUẢN LÝ THÔNG TIN CÁ NHÂN (USER PROFILE)
        // =========================================================================
        [Authorize]
        [HttpGet]
        public async Task<IActionResult> Profile()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null)
            {
                return RedirectToAction("Login");
            }

            int userId = int.Parse(userIdClaim.Value);
            var user = await _context.Users
                .Include(u => u.Role)
                .Include(u => u.Customer)
                .FirstOrDefaultAsync(u => u.UserId == userId);

            if (user == null)
            {
                return RedirectToAction("Login");
            }

            return View(user);
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Profile(string fullName, string email, string phone)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null)
            {
                return RedirectToAction("Login");
            }

            int userId = int.Parse(userIdClaim.Value);
            var user = await _context.Users
                .Include(u => u.Role)
                .Include(u => u.Customer)
                .FirstOrDefaultAsync(u => u.UserId == userId);

            if (user == null)
            {
                return RedirectToAction("Login");
            }

            if (string.IsNullOrEmpty(fullName) || string.IsNullOrEmpty(email) || string.IsNullOrEmpty(phone))
            {
                TempData["ErrorMessage"] = "Họ tên, email và số điện thoại không được để trống.";
                return View(user);
            }

            phone = phone.Trim();
            if (!ValidateProfilePhone(phone, out var phoneError))
            {
                TempData["ErrorMessage"] = phoneError;
                user.Phone = phone;
                return View(user);
            }

            // Kiểm tra trùng email với tài khoản khác
            if (await _context.Users.AnyAsync(u => u.Email == email && u.UserId != userId))
            {
                TempData["ErrorMessage"] = "Email đã được sử dụng bởi một tài khoản khác.";
                return View(user);
            }

            // Kiểm tra trùng phone với tài khoản khác
            if (await _context.Users.AnyAsync(u => u.Phone == phone && u.UserId != userId))
            {
                TempData["ErrorMessage"] = "Số điện thoại đã được sử dụng bởi một tài khoản khác.";
                return View(user);
            }

            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    // Cập nhật bảng Users
                    user.FullName = fullName;
                    user.Email = email;
                    user.Phone = phone;

                    _context.Entry(user).State = EntityState.Modified;

                    // Nếu là Customer, cập nhật bảng Customers
                    if (user.RoleId == 5)
                    {
                        var customer = await _context.Customers.FirstOrDefaultAsync(c => c.UserId == userId);
                        if (customer != null)
                        {
                            customer.FullName = fullName;
                            customer.Email = email;
                            customer.Phone = phone;
                            _context.Entry(customer).State = EntityState.Modified;
                        }
                    }

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    // Cập nhật lại Identity Claims của session hiện tại (đổi tên hiển thị)
                    var identity = (ClaimsIdentity)User.Identity!;
                    var fullNameClaim = identity.FindFirst("FullName");
                    if (fullNameClaim != null)
                    {
                        identity.RemoveClaim(fullNameClaim);
                        identity.AddClaim(new Claim("FullName", fullName));
                    }
                    var emailClaim = identity.FindFirst(ClaimTypes.Email);
                    if (emailClaim != null)
                    {
                        identity.RemoveClaim(emailClaim);
                        identity.AddClaim(new Claim(ClaimTypes.Email, email));
                    }

                    // Ký lại cookie để cập nhật thông tin session mới
                    await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));

                    TempData["SuccessMessage"] = "Cập nhật thông tin cá nhân thành công!";
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    TempData["ErrorMessage"] = "Có lỗi xảy ra: " + ex.Message;
                }
            }

            return View(user);
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UploadAvatar(IFormFile avatarFile)
        {
            if (avatarFile == null || avatarFile.Length == 0)
            {
                return Json(new { success = false, message = "Không tìm thấy file ảnh tải lên." });
            }

            // 1. Validate File Size: max 10MB
            long maxBytes = 10 * 1024 * 1024; // 10MB
            if (avatarFile.Length > maxBytes)
            {
                return Json(new { success = false, message = "Dung lượng ảnh vượt quá giới hạn cho phép (tối đa 10MB)." });
            }

            // 2. Validate MIME Type & Magic Bytes
            var contentType = avatarFile.ContentType.ToLower();
            if (contentType != "image/jpeg" && contentType != "image/png")
            {
                return Json(new { success = false, message = "Định dạng file không được hỗ trợ. Chỉ chấp nhận ảnh JPG hoặc PNG." });
            }

            // Verify Magic Bytes to prevent extension renaming attacks
            byte[] header = new byte[4];
            using (var stream = avatarFile.OpenReadStream())
            {
                await stream.ReadAsync(header, 0, 4);
            }

            bool isPng = header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47;
            bool isJpeg = header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF;

            if (!isPng && !isJpeg)
            {
                return Json(new { success = false, message = "Định dạng ảnh không hợp lệ (không khớp signature JPG/PNG)." });
            }

            // 3. Save File
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null)
            {
                return Json(new { success = false, message = "Phiên đăng nhập không hợp lệ." });
            }
            int userId = int.Parse(userIdClaim.Value);

            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId);
            if (user == null)
            {
                return Json(new { success = false, message = "Người dùng không tồn tại." });
            }

            var extension = isPng ? ".png" : ".jpg";
            var fileName = $"{userId}_{DateTime.UtcNow.Ticks}{extension}";
            
            var uploadsDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "avatars");
            if (!Directory.Exists(uploadsDir))
            {
                Directory.CreateDirectory(uploadsDir);
            }

            var filePath = Path.Combine(uploadsDir, fileName);
            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                await avatarFile.CopyToAsync(fileStream);
            }

            // 4. Update Database
            var dbPath = $"/uploads/avatars/{fileName}";
            user.AvatarPath = dbPath;
            _context.Entry(user).State = EntityState.Modified;
            await _context.SaveChangesAsync();

            return Json(new { success = true, avatarPath = dbPath });
        }

        // =========================================================================
        // 6. ĐỔI MẬT KHẨU (CHANGE PASSWORD)
        // =========================================================================
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(string currentPassword, string newPassword, string confirmNewPassword)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null)
            {
                return RedirectToAction("Login");
            }

            int userId = int.Parse(userIdClaim.Value);
            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId);

            if (user == null)
            {
                return RedirectToAction("Login");
            }

            if (string.IsNullOrEmpty(currentPassword) || string.IsNullOrEmpty(newPassword) || string.IsNullOrEmpty(confirmNewPassword))
            {
                TempData["PasswordError"] = "Vui lòng nhập đầy đủ các mật khẩu.";
                return RedirectToAction("Profile");
            }

            if (user.Password != currentPassword)
            {
                TempData["PasswordError"] = "Mật khẩu hiện tại không chính xác.";
                return RedirectToAction("Profile");
            }

            if (newPassword != confirmNewPassword)
            {
                TempData["PasswordError"] = "Mật khẩu mới và mật khẩu xác nhận không khớp.";
                return RedirectToAction("Profile");
            }

            if (!ValidatePasswordStrength(newPassword, out var passErr))
            {
                TempData["PasswordError"] = passErr;
                return RedirectToAction("Profile");
            }

            try
            {
                user.Password = newPassword;
                _context.Entry(user).State = EntityState.Modified;
                await _context.SaveChangesAsync();

                TempData["PasswordSuccess"] = "Đổi mật khẩu thành công!";
            }
            catch (Exception ex)
            {
                TempData["PasswordError"] = "Lỗi khi lưu mật khẩu: " + ex.Message;
            }

            return RedirectToAction("Profile");
        }

        // =========================================================================
        // 7. TRANG TỪ CHỐI TRUY CẬP (ACCESS DENIED)
        // =========================================================================
        [HttpGet]
        public IActionResult AccessDenied()
        {
            return View();
        }

        // =========================================================================
        // HELPER: ĐIỀU HƯỚNG THEO VAI TRÒ (DASHBOARD REDIRECT)
        // =========================================================================
        // Overload used right after login (User principal not yet refreshed on current request)
        private IActionResult RedirectToDashboard(string roleName)
        {
            return roleName.ToLowerInvariant() switch
            {
                "admin"     => RedirectToAction("Index", "Home", new { area = "Admin" }),
                "cashier"   => RedirectToAction("Index", "Home", new { area = "Cashier" }),
                "service"   => Redirect("/SpaServices"), // Redirect directly to SpaServices operational dashboard
                "warehouse" => RedirectToAction("Index", "Home", new { area = "Warehouse" }),
                "manager"   => RedirectToAction("Index", "Order", new { area = "Manager" }),
                // customer or any other role → public home
                _           => RedirectToAction("Index", "Home", new { area = "" })
            };
        }

        // Overload used when user is already authenticated (e.g. hitting /Login while logged in)
        private IActionResult RedirectToDashboard()
        {
            if (User.IsInRole("admin"))
                return RedirectToAction("Index", "Home", new { area = "Admin" });
            if (User.IsInRole("cashier"))
                return RedirectToAction("Index", "Home", new { area = "Cashier" });
            if (User.IsInRole("service"))
            {
                return Redirect("/SpaServices"); // Redirect directly to SpaServices operational dashboard
            }
            if (User.IsInRole("warehouse"))
                return RedirectToAction("Index", "Home", new { area = "Warehouse" });
            if (User.IsInRole("manager"))
                return RedirectToAction("Index", "Order", new { area = "Manager" });
            
            // Mặc định là customer hoặc vai trò khác -> về Trang chủ của Home ngoài Area
            return RedirectToAction("Index", "Home", new { area = "" });
        }
    }
}
