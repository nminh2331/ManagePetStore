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
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(string usernameOrEmail, string password, bool rememberMe, string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;

            if (string.IsNullOrEmpty(usernameOrEmail) || string.IsNullOrEmpty(password))
            {
                ModelState.AddModelError("", "Vui lòng nhập đầy đủ tên đăng nhập/email và mật khẩu.");
                return View();
            }

            // Tìm user bằng username hoặc email
            var user = await _context.Users
                .Include(u => u.Role)
                .Include(u => u.Customer)
                .FirstOrDefaultAsync(u => u.Username == usernameOrEmail || u.Email == usernameOrEmail);

            if (user == null || user.Password != password) // Plain-text password check
            {
                ModelState.AddModelError("", "Tên đăng nhập hoặc mật khẩu không chính xác.");
                return View();
            }

            if (user.Status != "Active")
            {
                ModelState.AddModelError("", "Tài khoản của bạn đã bị khóa hoặc ngưng hoạt động.");
                return View();
            }

            // Cấu hình Cookie Claims
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, user.Username),
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

            // Pass the role name directly — User.IsInRole() is unreliable on the same
            // request immediately after SignInAsync() because the principal hasn't been
            // refreshed yet from the new cookie.
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
        public async Task<IActionResult> Register(string username, string fullName, string email, string phone, string password, string confirmPassword)
        {
            if (!ValidateRegistrationInput(username, fullName, email, phone, password, confirmPassword))
            {
                return View();
            }

            try
            {
                if (await _context.Users.AnyAsync(u => u.Username == username))
                {
                    ModelState.AddModelError("", "Tên đăng nhập đã tồn tại trong hệ thống.");
                    return View();
                }

                if (await _context.Users.AnyAsync(u => u.Email == email) || await _context.Customers.AnyAsync(c => c.Email == email))
                {
                    ModelState.AddModelError("", "Email đã được sử dụng bởi tài khoản khác.");
                    return View();
                }

                if (await _context.Users.AnyAsync(u => u.Phone == phone) || await _context.Customers.AnyAsync(c => c.Phone == phone))
                {
                    ModelState.AddModelError("", "Số điện thoại đã được sử dụng bởi tài khoản khác.");
                    return View();
                }

                var otpCode = GenerateOtpCode();
                var pending = new PendingRegistration
                {
                    Username = username.Trim(),
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
                return View();
            }
            catch (Exception)
            {
                ModelState.AddModelError("", "Không thể gửi email OTP. Vui lòng kiểm tra cấu hình Gmail trong appsettings.json và thử lại.");
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
                if (await _context.Users.AnyAsync(u => u.Username == pending.Username))
                {
                    HttpContext.Session.Remove(PendingRegistrationSessionKey);
                    TempData["ErrorMessage"] = "Tên đăng nhập đã tồn tại. Vui lòng đăng ký lại.";
                    return RedirectToAction(nameof(Register));
                }

                if (await _context.Users.AnyAsync(u => u.Email == pending.Email))
                {
                    HttpContext.Session.Remove(PendingRegistrationSessionKey);
                    TempData["ErrorMessage"] = "Email đã được sử dụng. Vui lòng đăng ký lại.";
                    return RedirectToAction(nameof(Register));
                }

                await using var transaction = await _context.Database.BeginTransactionAsync();

                var newUser = new User
                {
                    Username = pending.Username,
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

        private bool ValidateRegistrationInput(string username, string fullName, string email, string phone, string password, string confirmPassword)
        {
            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(fullName) || string.IsNullOrEmpty(email) || string.IsNullOrEmpty(phone) || string.IsNullOrEmpty(password))
            {
                ModelState.AddModelError("", "Vui lòng nhập đầy đủ các trường thông tin bắt buộc.");
                return false;
            }

            if (password != confirmPassword)
            {
                ModelState.AddModelError("", "Mật khẩu và xác nhận mật khẩu không khớp.");
                return false;
            }

            if (username.Length < 1 || username.Length >= 20)
            {
                ModelState.AddModelError("", "Tên đăng nhập phải từ 1 đến 19 ký tự.");
            }

            if (fullName.Any(char.IsDigit))
            {
                ModelState.AddModelError("", "Họ và tên không được chứa chữ số.");
            }

            var emailRegex = new System.Text.RegularExpressions.Regex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$");
            if (!emailRegex.IsMatch(email))
            {
                ModelState.AddModelError("", "Email không đúng định dạng.");
            }

            if (password.Length < 1 || password.Length >= 20)
            {
                ModelState.AddModelError("", "Mật khẩu phải từ 1 đến 19 ký tự.");
            }

            return ModelState.IsValid;
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

            if (newPassword.Length < 1 || newPassword.Length >= 20)
            {
                ModelState.AddModelError("", "Mật khẩu phải từ 1 đến 19 ký tự.");
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
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
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

            // Mặc định là customer hoặc vai trò khác -> về Trang chủ của Home ngoài Area
            return RedirectToAction("Index", "Home", new { area = "" });
        }
    }
}
