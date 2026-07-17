/**
 * Project: Pet Store Management System (PSMS)
 * File: HomeController.cs
 * Author: Tran Duong
 * Date: May 31, 2026
 * Last Update: July 17, 2026
 * Description: Tá»± Ä‘á»™ng chuyá»ƒn hÆ°á»›ng ngÆ°á»i dÃ¹ng Ä‘áº¿n trang quáº£n lÃ½ sáº£n pháº©m.
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
