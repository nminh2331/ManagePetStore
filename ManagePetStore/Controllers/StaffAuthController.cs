using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ManagePetStore.Models;
using ManagePetStore.Services;
using ManagePetStore.Models.CustomerModels;

namespace ManagePetStore.Controllers
{
    [Route("Staff")]
    public class StaffAuthController : Controller
    {
        private const string PendingPasswordResetSessionKey = "StaffPendingPasswordReset";
        private const int OtpExpiryMinutes = 10;

        private readonly PetStoreManagementContext _context;
        private readonly IEmailService _emailService;

        public StaffAuthController(PetStoreManagementContext context, IEmailService emailService)
        {
            _context = context;
            _emailService = emailService;
        }

        // =========================================================================
        // 1. ĐĂNG NHẬP (LOGIN)
        // =========================================================================
        [HttpGet("Login")]
        public IActionResult Login(string? returnUrl = null)
        {
            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                if (!User.IsInRole("customer"))
                {
                    return RedirectToDashboard();
                }
            }
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        [HttpPost("Login")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(string usernameOrEmail, string password, bool rememberMe, string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;

            if (string.IsNullOrEmpty(usernameOrEmail) || string.IsNullOrEmpty(password))
            {
                ModelState.AddModelError("", "Vui lòng nhập đầy đủ email và mật khẩu.");
                return View();
            }

            var user = await _context.Users
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.Email == usernameOrEmail);

            if (user == null || user.Password != password)
            {
                ModelState.AddModelError("", "Email hoặc mật khẩu không chính xác.");
                return View();
            }

            if (user.Status != "Active")
            {
                ModelState.AddModelError("", "Tài khoản của bạn đã bị khóa hoặc ngưng hoạt động.");
                return View();
            }

            // Chặn tài khoản Khách hàng đăng nhập tại cổng Nhân viên
            if (user.RoleId == 5)
            {
                ModelState.AddModelError("", "Tài khoản khách hàng không được phép đăng nhập tại trang dành cho nhân viên.");
                return View();
            }

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
        // 2. ĐĂNG XUẤT (LOGOUT)
        // =========================================================================
        [HttpPost("Logout")]
        [HttpGet("Logout")]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Login");
        }

        // =========================================================================
        // 3. QUÊN MẬT KHẨU (FORGOT PASSWORD)
        // =========================================================================
        [HttpGet("ForgotPassword")]
        public IActionResult ForgotPassword()
        {
            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                if (!User.IsInRole("customer"))
                {
                    return RedirectToDashboard();
                }
            }

            return View(new ForgotPasswordViewModel());
        }

        [HttpPost("ForgotPassword")]
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
                if (user == null || user.RoleId == 5) // Chỉ cho phép Nhân viên (RoleId != 5) khôi phục
                {
                    ModelState.AddModelError("", "Tài khoản không hợp lệ hoặc không có quyền nhân viên.");
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

        [HttpGet("ForgotPasswordVerifyOtp")]
        public IActionResult ForgotPasswordVerifyOtp()
        {
            var pending = GetPendingPasswordReset();
            if (pending == null)
            {
                return RedirectToAction(nameof(ForgotPassword));
            }

            return View(new ForgotPasswordVerifyOtpViewModel { Email = pending.Email });
        }

        [HttpPost("ForgotPasswordVerifyOtp")]
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

        [HttpPost("ResendForgotPasswordOtp")]
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

        [HttpGet("ResetPassword")]
        public IActionResult ResetPassword()
        {
            var pending = GetPendingPasswordReset();
            if (pending == null || !pending.OtpVerified)
            {
                return RedirectToAction(nameof(ForgotPassword));
            }

            return View(new ResetPasswordViewModel { Email = pending.Email });
        }

        [HttpPost("ResetPassword")]
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

        // =========================================================================
        // HELPERS
        // =========================================================================
        private static string GenerateOtpCode()
        {
            return Random.Shared.Next(100000, 999999).ToString();
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
            var subject = "Mã xác thực đặt lại mật khẩu nhân viên PetStore";
            var body = $@"
                <div style=""font-family: Arial, sans-serif; max-width: 520px; margin: 0 auto;"">
                    <h2 style=""color: #f97316;"">PetStore - Đặt lại mật khẩu nhân viên</h2>
                    <p>Xin chào <strong>{fullName}</strong>,</p>
                    <p>Bạn đã yêu cầu đặt lại mật khẩu cho tài khoản nhân viên. Mã OTP của bạn là:</p>
                    <p style=""font-size: 32px; font-weight: bold; letter-spacing: 8px; color: #3d2314;"">{otpCode}</p>
                    <p>Mã có hiệu lực trong <strong>{OtpExpiryMinutes} phút</strong>. Không chia sẻ mã này với bất kỳ ai.</p>
                    <p style=""color: #6b7280; font-size: 13px;"">Nếu bạn không yêu cầu đặt lại mật khẩu, hãy bỏ qua email này.</p>
                </div>";

            await _emailService.SendEmailAsync(email, subject, body);
        }

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

        // =========================================================================
        // DASHBOARD REDIRECT HELPERS
        // =========================================================================
        private IActionResult RedirectToDashboard(string roleName)
        {
            return roleName.ToLowerInvariant() switch
            {
                "admin"     => RedirectToAction("Index", "AdminHome"),
                "cashier"   => RedirectToAction("Index", "CashierHome"),
                "service"   => Redirect("/SpaServices"),
                "warehouse" => RedirectToAction("Index", "WarehouseHome"),
                "manager"   => RedirectToAction("Index", "ManagerOrder"),
                _           => RedirectToAction("Index", "Home", new { area = "" })
            };
        }

        private IActionResult RedirectToDashboard()
        {
            if (User.IsInRole("admin"))
                return RedirectToAction("Index", "AdminHome");
            if (User.IsInRole("cashier"))
                return RedirectToAction("Index", "CashierHome");
            if (User.IsInRole("service"))
                return Redirect("/SpaServices");
            if (User.IsInRole("warehouse"))
                return RedirectToAction("Index", "WarehouseHome");
            if (User.IsInRole("manager"))
                return RedirectToAction("Index", "ManagerOrder");

            return RedirectToAction("Index", "Home", new { area = "" });
        }
    }
}

