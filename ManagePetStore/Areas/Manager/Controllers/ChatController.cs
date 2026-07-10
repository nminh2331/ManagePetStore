using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ManagePetStore.Areas.Manager.Controllers;

[Area("Manager")]
[Authorize(Roles = "manager,admin")]
public class ChatController : Controller
{
    // =========================================================================
    // INDEX - Trang chủ Live Chat Dashboard của Manager
    // =========================================================================
    public IActionResult Index()
    {
        // Truyền các thông tin ID người dùng hiện tại sang View để xử lý JS
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        ViewBag.CurrentUserId = userIdStr != null ? int.Parse(userIdStr) : 0;
        ViewBag.CurrentUserName = User.FindFirst("FullName")?.Value ?? "Manager";

        return View();
    }
}
