using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ManagePetStore.Models;

namespace ManagePetStore.Areas.Customer.Controllers
{
    [Area("Customer")]
    public class AccountController : Controller
    {
        private readonly PetStoreManagementContext _context;

        public AccountController(PetStoreManagementContext context)
        {
            _context = context;
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
            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(fullName) || string.IsNullOrEmpty(email) || string.IsNullOrEmpty(phone) || string.IsNullOrEmpty(password))
            {
                ModelState.AddModelError("", "Vui lòng nhập đầy đủ các trường thông tin bắt buộc.");
                return View();
            }

            if (password != confirmPassword)
            {
                ModelState.AddModelError("", "Mật khẩu và xác nhận mật khẩu không khớp.");
                return View();
            }

            // 1. Validate Username length (0 < length < 20)
            if (username.Length < 1 || username.Length >= 20)
            {
                ModelState.AddModelError("", "Tên đăng nhập phải từ 1 đến 19 ký tự.");
            }

            // 2. Validate Họ và tên (không chứa số)
            if (fullName.Any(char.IsDigit))
            {
                ModelState.AddModelError("", "Họ và tên không được chứa chữ số.");
            }

            // 3. Validate Email đúng cú pháp
            var emailRegex = new System.Text.RegularExpressions.Regex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$");
            if (!emailRegex.IsMatch(email))
            {
                ModelState.AddModelError("", "Email không đúng định dạng.");
            }

            // 4. Validate Mật khẩu (0 < length < 20)
            if (password.Length < 1 || password.Length >= 20)
            {
                ModelState.AddModelError("", "Mật khẩu phải từ 1 đến 19 ký tự.");
            }

            if (!ModelState.IsValid)
            {
                return View();
            }

            // Kiểm tra trùng username
            if (await _context.Users.AnyAsync(u => u.Username == username))
            {
                ModelState.AddModelError("", "Tên đăng nhập đã tồn tại trong hệ thống.");
                return View();
            }

            // Kiểm tra trùng email
            if (await _context.Users.AnyAsync(u => u.Email == email) || await _context.Customers.AnyAsync(c => c.Email == email))
            {
                ModelState.AddModelError("", "Email đã được sử dụng bởi tài khoản khác.");
                return View();
            }

            // Kiểm tra trùng số điện thoại
            if (await _context.Users.AnyAsync(u => u.Phone == phone) || await _context.Customers.AnyAsync(c => c.Phone == phone))
            {
                ModelState.AddModelError("", "Số điện thoại đã được sử dụng bởi tài khoản khác.");
                return View();
            }

            // Tiến hành lưu dữ liệu dùng Transaction
            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    // 1. Lưu User mới (Role 5 = customer)
                    var newUser = new User
                    {
                        Username = username,
                        Password = password, // Plain-text
                        FullName = fullName,
                        Email = email,
                        Phone = phone,
                        RoleId = 5, // customer
                        Status = "Active",
                        CreatedAt = DateTime.Now
                    };

                    _context.Users.Add(newUser);
                    await _context.SaveChangesAsync(); // Lưu để sinh ra UserId

                    // 2. Lưu Customer mới liên kết với User vừa tạo (sử dụng fully qualified name tránh collision)
                    var newCustomer = new ManagePetStore.Models.Customer
                    {
                        UserId = newUser.UserId,
                        FullName = fullName,
                        Phone = phone,
                        Email = email,
                        LoyaltyPoints = 0,
                        MembershipTier = "Bronze",
                        CreatedAt = DateTime.Now
                    };

                    _context.Customers.Add(newCustomer);
                    await _context.SaveChangesAsync();

                    await transaction.CommitAsync();

                    TempData["SuccessMessage"] = "Đăng ký tài khoản thành công! Vui lòng đăng nhập.";
                    return RedirectToAction("Login");
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    ModelState.AddModelError("", "Có lỗi xảy ra trong quá trình đăng ký. Vui lòng thử lại sau. Chi tiết: " + ex.Message);
                    return View();
                }
            }
        }

        // =========================================================================
        // 3. ĐĂNG XUẤT (LOGOUT)
        // =========================================================================
        [HttpPost]
        [HttpGet]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Login");
        }

        // =========================================================================
        // 4. QUẢN LÝ THÔNG TIN CÁ NHÂN (USER PROFILE)
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
        // 5. ĐỔI MẬT KHẨU (CHANGE PASSWORD)
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
        // 6. TRANG TỪ CHỐI TRUY CẬP (ACCESS DENIED)
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
                "service"   => RedirectToAction("Index", "Home", new { area = "Service" }),
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
                return RedirectToAction("Index", "Home", new { area = "Service" });
            if (User.IsInRole("warehouse"))
                return RedirectToAction("Index", "Home", new { area = "Warehouse" });

            // Mặc định là customer hoặc vai trò khác -> về Trang chủ của Home ngoài Area
            return RedirectToAction("Index", "Home", new { area = "" });
        }
    }
}
