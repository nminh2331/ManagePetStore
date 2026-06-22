/**
 * Project: Pet Store Management System (PSMS)
 * File: HomeController.cs
 * Author: Tran Duong
 * Date: May 31, 2026
 * Description: Tự động chuyển hướng người dùng đến trang quản lý sản phẩm.
 */
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

/**
 * Project: Pet Store Management System (PSMS)
 * File: HomeController.cs
 * Author: Tran Duong
 * Date: May 31, 2026
 * Description: Tự động chuyển hướng người dùng đến trang quản lý sản phẩm.
 */
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ManagePetStore.Controllers
{
    [Authorize(Roles = "warehouse")]
    [Route("Warehouse/Home/{action=Index}/{id?}")]
public class WarehouseHomeController : Controller
    {
        public IActionResult Index()
        {
            return RedirectToAction("Index", "WarehouseProduct");
        }
    }
}
