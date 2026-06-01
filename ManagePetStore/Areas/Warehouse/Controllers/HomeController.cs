/**
 * Project: Pet Store Management System (PSMS)
 * File: HomeController.cs
 * Author: Tran Duong
 * Date: May 31, 2026
 * Description: Tự động chuyển hướng người dùng đến trang quản lý sản phẩm.
 */
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ManagePetStore.Areas.Warehouse.Controllers
{
    [Area("Warehouse")]
    [Authorize(Roles = "warehouse")]
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            return RedirectToAction("Index", "Product");
        }
    }
}
