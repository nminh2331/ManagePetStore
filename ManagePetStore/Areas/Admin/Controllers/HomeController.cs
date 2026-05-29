using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ManagePetStore.Models;

namespace ManagePetStore.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "admin")]
    public class HomeController : Controller
    {
        private readonly PetStoreManagementContext _context;

        public HomeController(PetStoreManagementContext context)
        {
            _context = context;
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
                staffQuery = staffQuery.Where(u => u.FullName.Contains(searchStaff) || (u.Email != null && u.Email.Contains(searchStaff)) || u.Username.Contains(searchStaff));
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
                customerQuery = customerQuery.Where(u => u.FullName.Contains(searchCustomer) || (u.Email != null && u.Email.Contains(searchCustomer)) || u.Username.Contains(searchCustomer));
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
                TempData["SuccessMessage"] = $"Đã cập nhật trạng thái tài khoản {user.Username} thành công.";
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

                    TempData["SuccessMessage"] = $"Đã xóa tài khoản {user.Username} ra khỏi hệ thống.";
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

            // 1. Họ và tên không chứa số
            if (fullName.Any(char.IsDigit))
            {
                TempData["ErrorMessage"] = "Họ và tên không được chứa chữ số.";
                return RedirectToAction("Index", new { activeTab = "staff" });
            }

            // 2. Email đúng định dạng
            var emailRegex = new System.Text.RegularExpressions.Regex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$");
            if (!emailRegex.IsMatch(email))
            {
                TempData["ErrorMessage"] = "Email không đúng định dạng.";
                return RedirectToAction("Index", new { activeTab = "staff" });
            }

            // 3. Mật khẩu trên 0 dưới 20 ký tự
            if (password.Length < 1 || password.Length >= 20)
            {
                TempData["ErrorMessage"] = "Mật khẩu phải từ 1 đến 19 ký tự.";
                return RedirectToAction("Index", new { activeTab = "staff" });
            }

            // Sinh username tự động từ email (lấy phần trước dấu @)
            string username = email.Split('@')[0];
            
            // Validate Username length (0 < length < 20)
            if (username.Length < 1 || username.Length >= 20)
            {
                TempData["ErrorMessage"] = "Tên đăng nhập tự động sinh từ email phải từ 1 đến 19 ký tự.";
                return RedirectToAction("Index", new { activeTab = "staff" });
            }

            // Đảm bảo username không trùng lặp
            int count = 1;
            string baseUsername = username;
            while (await _context.Users.AnyAsync(u => u.Username == username))
            {
                username = baseUsername + count;
                count++;
            }

            // Kiểm tra trùng email
            if (await _context.Users.AnyAsync(u => u.Email == email))
            {
                TempData["ErrorMessage"] = "Email này đã được đăng ký trong hệ thống.";
                return RedirectToAction("Index", new { activeTab = "staff" });
            }

            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    // 1. Tạo User mới
                    var newUser = new User
                    {
                        Username = username,
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

                    // 2. Tạo StaffProfile liên kết để tránh lỗi khóa ngoại
                    var staffProfile = new StaffProfile
                    {
                        UserId = newUser.UserId,
                        PerformanceScore = 100,
                        TasksCompletedCount = 0,
                        RatingsAverage = 5.00m
                    };

                    _context.StaffProfiles.Add(staffProfile);
                    await _context.SaveChangesAsync();

                    await transaction.CommitAsync();
                    TempData["SuccessMessage"] = $"Đã cấp tài khoản nhân viên thành công cho {fullName} (Username: {username}).";
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

            // 1. Họ và tên không chứa số
            if (fullName.Any(char.IsDigit))
            {
                TempData["ErrorMessage"] = "Họ và tên không được chứa chữ số.";
                return RedirectToAction("Index", new { activeTab = "staff" });
            }

            // 2. Mật khẩu trên 0 dưới 20 ký tự
            if (password.Length < 1 || password.Length >= 20)
            {
                TempData["ErrorMessage"] = "Mật khẩu phải từ 1 đến 19 ký tự.";
                return RedirectToAction("Index", new { activeTab = "staff" });
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId);
            if (user == null || user.RoleId == 5)
            {
                TempData["ErrorMessage"] = "Tài khoản nhân viên không tồn tại.";
                return RedirectToAction("Index", new { activeTab = "staff" });
            }

            try
            {
                user.FullName = fullName;
                user.Phone = phone;
                user.Password = password;

                _context.Entry(user).State = EntityState.Modified;
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = $"Đã cập nhật tài khoản nhân viên {user.Username} thành công.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Có lỗi xảy ra khi cập nhật nhân viên: " + ex.Message;
            }

            return RedirectToAction("Index", new { activeTab = "staff" });
        }
    }
}
