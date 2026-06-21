using ManagePetStore.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ManagePetStore.Controllers
{
    [Authorize(Roles = "admin")]
    [Route("Admin/Home/{action=Index}/{id?}")]
    public class AdminHomeController : Controller
    {
        private readonly PetStoreManagementContext _context;

        public AdminHomeController(PetStoreManagementContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(string activeTab = "staff")
        {
            ViewBag.ActiveTab = activeTab;

            var staffList = await _context.Users
                .Include(u => u.Role)
                .Where(u => u.Role != null && u.Role.RoleName != "customer")
                .OrderBy(u => u.FullName)
                .ToListAsync();

            var customerList = await _context.Users
                .Include(u => u.Role)
                .Where(u => u.Role != null && u.Role.RoleName == "customer")
                .OrderBy(u => u.FullName)
                .ToListAsync();

            var rolesList = await _context.Roles.ToListAsync();

            ViewBag.StaffList     = staffList;
            ViewBag.CustomerList  = customerList;
            ViewBag.RolesList     = rolesList;

            return View();
        }

        [HttpPost]
        public async Task<IActionResult> CreateStaff(string fullName, string email, string password, int roleId)
        {
            var user = new User
            {
                FullName  = fullName,
                Email     = email,
                Password  = password,
                RoleId    = roleId,
                CreatedAt = DateTime.UtcNow
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Tạo tài khoản nhân viên thành công.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> UpdateRole(int userId, int roleId)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user != null)
            {
                user.RoleId = roleId;
                await _context.SaveChangesAsync();
                TempData["Success"] = "Cập nhật vai trò thành công.";
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> DeleteUser(int userId)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user != null)
            {
                _context.Users.Remove(user);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Xóa tài khoản thành công.";
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
