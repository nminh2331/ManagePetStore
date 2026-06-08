using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ManagePetStore.Models;
using System.Text.Json;
using ManagePetStore.Services;
using ManagePetStore.Areas.Admin.Models;

namespace ManagePetStore.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "admin")]
    public class HomeController : Controller
    {
        private readonly PetStoreManagementContext _context;
        private readonly IEmailService _emailService;
        private const string PendingStaffSessionKey = "PendingStaffCreation";
        private const int OtpExpiryMinutes = 10;

        public HomeController(PetStoreManagementContext context, IEmailService emailService)
        {
            _context = context;
            _emailService = emailService;
        }

        // =========================================================================
        // 1. DANH SÁCH & BỘ LỌC (INDEX)
        // =========================================================================
        [HttpGet]
        public async Task<IActionResult> Index(
            string activeTab = "staff", 
            string searchStaff = "", 
            string searchCustomer = "", 
            string roleFilter = "All", 
            string statusFilter = "All")
        {
            ViewBag.ActiveTab = activeTab;
            ViewBag.SearchStaff = searchStaff;
            ViewBag.SearchCustomer = searchCustomer;
            ViewBag.RoleFilter = roleFilter;
            ViewBag.StatusFilter = statusFilter;

            // --- LẤY DANH SÁCH NHÂN VIÊN (RoleId != 5) ---
            var staffQuery = _context.Users
                .Include(u => u.Role)
                .Where(u => u.RoleId != 5);

            // Thống kê nhân viên trước khi lọc
            var totalStaffList = await staffQuery.ToListAsync();
            ViewBag.StaffCount = totalStaffList.Count;
            ViewBag.StaffActive = totalStaffList.Count(u => u.Status == "Active");
            ViewBag.StaffLocked = totalStaffList.Count(u => u.Status == "disabled");

            // Lọc nhân viên theo tìm kiếm
            if (!string.IsNullOrEmpty(searchStaff))
            {
                staffQuery = staffQuery.Where(u => u.FullName.Contains(searchStaff) || (u.Email != null && u.Email.Contains(searchStaff)));
            }

            // Lọc nhân viên theo vai trò
            if (roleFilter != "All")
            {
                int rId = int.Parse(roleFilter);
                staffQuery = staffQuery.Where(u => u.RoleId == rId);
            }

            // Lọc nhân viên theo trạng thái
            if (statusFilter != "All")
            {
                staffQuery = staffQuery.Where(u => u.Status == statusFilter);
            }

            ViewBag.StaffList = await staffQuery.OrderBy(u => u.UserId).ToListAsync();


            // --- LẤY DANH SÁCH KHÁCH HÀNG (RoleId == 5) ---
            var customerQuery = _context.Users
                .Include(u => u.Role)
                .Include(u => u.Customer)
                .Where(u => u.RoleId == 5);

            // Thống kê khách hàng trước khi lọc
            var totalCustomerList = await customerQuery.ToListAsync();
            ViewBag.CustomerCount = totalCustomerList.Count;
            ViewBag.CustomerActive = totalCustomerList.Count(u => u.Status == "Active");
            ViewBag.CustomerLocked = totalCustomerList.Count(u => u.Status == "disabled");

            // Lọc khách hàng theo tìm kiếm
            if (!string.IsNullOrEmpty(searchCustomer))
            {
                customerQuery = customerQuery.Where(u => u.FullName.Contains(searchCustomer) || (u.Email != null && u.Email.Contains(searchCustomer)));
            }

            // Lọc khách hàng theo trạng thái
            if (statusFilter != "All")
            {
                customerQuery = customerQuery.Where(u => u.Status == statusFilter);
            }

            ViewBag.CustomerList = await customerQuery.OrderBy(u => u.UserId).ToListAsync();

            // Load danh sách vai trò cho form tạo nhân viên
            ViewBag.RolesList = await _context.Roles.Where(r => r.RoleId != 5).ToListAsync();

            return View();
        }

        // =========================================================================
        // 2. KHÓA / MỞ KHÓA TÀI KHOẢN
        // =========================================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleStatus(int userId, string activeTab)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId);
            if (user != null)
            {
                user.Status = (user.Status == "Active") ? "disabled" : "Active";
                _context.Entry(user).State = EntityState.Modified;
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"Đã cập nhật trạng thái tài khoản {user.Email} thành công.";
            }
            return RedirectToAction("Index", new { activeTab = activeTab });
        }

        // =========================================================================
        // 3. XÓA TÀI KHOẢN
        // =========================================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteUser(int userId, string activeTab)
        {
            var user = await _context.Users
                .Include(u => u.Customer)
                .Include(u => u.StaffProfile)
                .FirstOrDefaultAsync(u => u.UserId == userId);

            if (user == null)
            {
                return RedirectToAction("Index", new { activeTab = activeTab });
            }

            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    // Nếu là customer, xóa bản ghi customer
                    if (user.RoleId == 5)
                    {
                        var customer = await _context.Customers.FirstOrDefaultAsync(c => c.UserId == userId);
                        if (customer != null)
                        {
                            _context.Customers.Remove(customer);
                        }
                    }
                    // Nếu là staff, xóa staff profile
                    else
                    {
                        var staffProfile = await _context.StaffProfiles.FirstOrDefaultAsync(sp => sp.UserId == userId);
                        if (staffProfile != null)
                        {
                            _context.StaffProfiles.Remove(staffProfile);
                        }
                    }

                    // Xóa user
                    _context.Users.Remove(user);
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    TempData["SuccessMessage"] = $"Đã xóa tài khoản {user.Email} ra khỏi hệ thống.";
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    TempData["ErrorMessage"] = $"Không thể xóa tài khoản này vì có các dữ liệu liên quan khác trong hệ thống. Lỗi: {ex.Message}";
                }
            }

            return RedirectToAction("Index", new { activeTab = activeTab });
        }

        // =========================================================================
        // 4. CẤP TÀI KHOẢN NHÂN VIÊN
        // =========================================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateStaff(string fullName, string email, string phone, int roleId, string password)
        {
            if (string.IsNullOrEmpty(fullName) || string.IsNullOrEmpty(email) || string.IsNullOrEmpty(phone) || string.IsNullOrEmpty(password) || roleId <= 0)
            {
                TempData["ErrorMessage"] = "Vui lòng nhập đầy đủ thông tin bắt buộc.";
                return RedirectToAction("Index", new { activeTab = "staff" });
            }

            if (fullName.Any(char.IsDigit))
            {
                TempData["ErrorMessage"] = "Họ và tên không được chứa chữ số.";
                return RedirectToAction("Index", new { activeTab = "staff" });
            }

            var emailRegex = new System.Text.RegularExpressions.Regex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$");
            if (!emailRegex.IsMatch(email))
            {
                TempData["ErrorMessage"] = "Email không đúng định dạng.";
                return RedirectToAction("Index", new { activeTab = "staff" });
            }

            if (!ValidatePhone(phone, out var phoneErr))
            {
                TempData["ErrorMessage"] = phoneErr;
                return RedirectToAction("Index", new { activeTab = "staff" });
            }

            if (!ValidatePasswordStrength(password, out var passErr))
            {
                TempData["ErrorMessage"] = passErr;
                return RedirectToAction("Index", new { activeTab = "staff" });
            }

            if (await _context.Users.AnyAsync(u => u.Email == email) || await _context.Customers.AnyAsync(c => c.Email == email))
            {
                TempData["ErrorMessage"] = "Email này đã được đăng ký trong hệ thống.";
                return RedirectToAction("Index", new { activeTab = "staff" });
            }

            if (await _context.Users.AnyAsync(u => u.Phone == phone) || await _context.Customers.AnyAsync(c => c.Phone == phone))
            {
                TempData["ErrorMessage"] = "Số điện thoại này đã được đăng ký trong hệ thống.";
                return RedirectToAction("Index", new { activeTab = "staff" });
            }

            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    var newUser = new User
                    {
                        Password = password,
                        FullName = fullName,
                        Email = email,
                        Phone = phone,
                        RoleId = roleId,
                        Status = "Active",
                        CreatedAt = DateTime.Now
                    };

                    _context.Users.Add(newUser);
                    await _context.SaveChangesAsync();

                    if (roleId != 5)
                    {
                        var staffProfile = new StaffProfile
                        {
                            UserId = newUser.UserId,
                            PerformanceScore = 100,
                            TasksCompletedCount = 0,
                            RatingsAverage = 5.00m
                        };
                        _context.StaffProfiles.Add(staffProfile);
                        await _context.SaveChangesAsync();
                    }

                    await transaction.CommitAsync();
                    TempData["SuccessMessage"] = $"Đã cấp tài khoản nhân viên thành công cho {fullName} (Email: {email}).";
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    TempData["ErrorMessage"] = $"Có lỗi xảy ra khi tạo nhân viên: {ex.Message}";
                }
            }

            return RedirectToAction("Index", new { activeTab = "staff" });
        }

        // =========================================================================
        // AJAX: GỬI OTP CHO NHÂN VIÊN
        // =========================================================================
        [HttpPost]
        public async Task<IActionResult> SendStaffOtp(string fullName, string email, string phone, int roleId, string password)
        {
            if (string.IsNullOrEmpty(fullName) || string.IsNullOrEmpty(email) || string.IsNullOrEmpty(phone) || string.IsNullOrEmpty(password) || roleId <= 0)
            {
                return Json(new { success = false, message = "Vui lòng nhập đầy đủ thông tin bắt buộc." });
            }

            if (fullName.Any(char.IsDigit))
            {
                return Json(new { success = false, message = "Họ và tên không được chứa chữ số." });
            }

            var emailRegex = new System.Text.RegularExpressions.Regex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$");
            if (!emailRegex.IsMatch(email))
            {
                return Json(new { success = false, message = "Email không đúng định dạng." });
            }

            if (!ValidatePhone(phone, out var phoneErr))
            {
                return Json(new { success = false, message = phoneErr });
            }

            if (!ValidatePasswordStrength(password, out var passErr))
            {
                return Json(new { success = false, message = passErr });
            }

            if (await _context.Users.AnyAsync(u => u.Email == email) || await _context.Customers.AnyAsync(c => c.Email == email))
            {
                return Json(new { success = false, message = "Email này đã được đăng ký trong hệ thống." });
            }

            if (await _context.Users.AnyAsync(u => u.Phone == phone) || await _context.Customers.AnyAsync(c => c.Phone == phone))
            {
                return Json(new { success = false, message = "Số điện thoại này đã được đăng ký trong hệ thống." });
            }

            var otpCode = Random.Shared.Next(100000, 999999).ToString();
            var pending = new PendingStaffCreation
            {
                FullName = fullName.Trim(),
                Email = email.Trim(),
                Phone = phone.Trim(),
                RoleId = roleId,
                Password = password,
                OtpCode = otpCode,
                ExpiresAt = DateTime.UtcNow.AddMinutes(OtpExpiryMinutes)
            };

            var json = JsonSerializer.Serialize(pending);
            HttpContext.Session.SetString(PendingStaffSessionKey, json);

            var subject = "Mã xác thực tạo tài khoản nhân viên PetStore";
            var body = $@"
                <div style=""font-family: Arial, sans-serif; max-width: 520px; margin: 0 auto;"">
                    <h2 style=""color: #f97316;"">PetStore - Xác thực cấp tài khoản</h2>
                    <p>Xin chào <strong>{fullName}</strong>,</p>
                    <p>Bạn được cấp tài khoản nhân viên tại PetStore. Mã OTP xác nhận của bạn là:</p>
                    <p style=""font-size: 32px; font-weight: bold; letter-spacing: 8px; color: #3d2314;"">{otpCode}</p>
                    <p>Mã có hiệu lực trong <strong>{OtpExpiryMinutes} phút</strong>. Không chia sẻ mã này với bất kỳ ai.</p>
                </div>";

            try
            {
                await _emailService.SendEmailAsync(pending.Email, subject, body);
            }
            catch (Exception)
            {
                return Json(new { success = false, message = "Không thể gửi email OTP. Vui lòng kiểm tra cấu hình Gmail và thử lại." });
            }

            return Json(new { success = true, message = $"Mã OTP đã được gửi đến email {email}. Mã có hiệu lực trong {OtpExpiryMinutes} phút." });
        }

        // =========================================================================
        // AJAX: XÁC THỰC OTP & TẠO TÀI KHOẢN
        // =========================================================================
        [HttpPost]
        public async Task<IActionResult> VerifyStaffOtpAndCreate(string otpCode)
        {
            var json = HttpContext.Session.GetString(PendingStaffSessionKey);
            if (string.IsNullOrEmpty(json))
            {
                return Json(new { success = false, message = "Phiên cấp tài khoản đã hết hạn. Vui lòng thực hiện lại từ đầu." });
            }

            var pending = JsonSerializer.Deserialize<PendingStaffCreation>(json);
            if (pending == null)
            {
                return Json(new { success = false, message = "Phiên cấp tài khoản không hợp lệ. Vui lòng thử lại." });
            }

            if (string.IsNullOrWhiteSpace(otpCode))
            {
                return Json(new { success = false, message = "Vui lòng nhập mã OTP." });
            }

            if (DateTime.UtcNow > pending.ExpiresAt)
            {
                HttpContext.Session.Remove(PendingStaffSessionKey);
                return Json(new { success = false, message = "Mã OTP đã hết hạn. Vui lòng gửi lại mã mới." });
            }

            if (!string.Equals(pending.OtpCode, otpCode.Trim(), StringComparison.Ordinal))
            {
                return Json(new { success = false, message = "Mã OTP không chính xác. Vui lòng kiểm tra lại email." });
            }

            if (await _context.Users.AnyAsync(u => u.Email == pending.Email) || await _context.Customers.AnyAsync(c => c.Email == pending.Email))
            {
                HttpContext.Session.Remove(PendingStaffSessionKey);
                return Json(new { success = false, message = "Email đã được sử dụng. Vui lòng thử lại." });
            }

            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    var newUser = new User
                    {
                        Password = pending.Password,
                        FullName = pending.FullName,
                        Email = pending.Email,
                        Phone = pending.Phone,
                        RoleId = pending.RoleId,
                        Status = "Active",
                        CreatedAt = DateTime.Now
                    };

                    _context.Users.Add(newUser);
                    await _context.SaveChangesAsync();

                    if (pending.RoleId != 5)
                    {
                        var staffProfile = new StaffProfile
                        {
                            UserId = newUser.UserId,
                            PerformanceScore = 100,
                            TasksCompletedCount = 0,
                            RatingsAverage = 5.00m
                        };
                        _context.StaffProfiles.Add(staffProfile);
                        await _context.SaveChangesAsync();
                    }

                    await transaction.CommitAsync();
                    HttpContext.Session.Remove(PendingStaffSessionKey);

                    TempData["SuccessMessage"] = $"Đã cấp tài khoản nhân viên thành công cho {pending.FullName} (Email: {pending.Email}).";
                    return Json(new { success = true });
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    return Json(new { success = false, message = $"Có lỗi xảy ra khi tạo nhân viên: {ex.Message}" });
                }
            }
        }

        // =========================================================================
        // 5. CHỈNH SỬA TÀI KHOẢN NHÂN VIÊN
        // =========================================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditStaff(int userId, string fullName, string phone, string password)
        {
            if (string.IsNullOrEmpty(fullName) || string.IsNullOrEmpty(phone) || string.IsNullOrEmpty(password) || userId <= 0)
            {
                TempData["ErrorMessage"] = "Vui lòng nhập đầy đủ thông tin bắt buộc.";
                return RedirectToAction("Index", new { activeTab = "staff" });
            }

            if (fullName.Any(char.IsDigit))
            {
                TempData["ErrorMessage"] = "Họ và tên không được chứa chữ số.";
                return RedirectToAction("Index", new { activeTab = "staff" });
            }

            if (!ValidatePhone(phone, out var phoneErr))
            {
                TempData["ErrorMessage"] = phoneErr;
                return RedirectToAction("Index", new { activeTab = "staff" });
            }

            if (!ValidatePasswordStrength(password, out var passErr))
            {
                TempData["ErrorMessage"] = passErr;
                return RedirectToAction("Index", new { activeTab = "staff" });
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId);
            if (user == null || user.RoleId == 5)
            {
                TempData["ErrorMessage"] = "Tài khoản nhân viên không tồn tại.";
                return RedirectToAction("Index", new { activeTab = "staff" });
            }

            // Check duplicate phone with other users
            if (await _context.Users.AnyAsync(u => u.Phone == phone && u.UserId != userId) || 
                await _context.Customers.AnyAsync(c => c.Phone == phone && c.UserId != userId))
            {
                TempData["ErrorMessage"] = "Số điện thoại đã được sử dụng bởi tài khoản khác.";
                return RedirectToAction("Index", new { activeTab = "staff" });
            }

            try
            {
                user.FullName = fullName;
                user.Phone = phone;
                user.Password = password;

                _context.Entry(user).State = EntityState.Modified;
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = $"Đã cập nhật tài khoản nhân viên {user.Email} thành công.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Có lỗi xảy ra khi cập nhật nhân viên: " + ex.Message;
            }

            return RedirectToAction("Index", new { activeTab = "staff" });
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
            for (int i = 0; i < password.Length - 2; i++)
            {
                if (password[i] == password[i + 1] && password[i] == password[i + 2])
                {
                    errorMessage = "Mật khẩu không được chứa ký tự lặp lại liên tiếp (ví dụ: aaa).";
                    return false;
                }
            }
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
    }
}
