using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ManagePetStore.Areas.Cashier.Controllers
{
    [Area("Cashier")]
    [Authorize(Roles = "admin,manager")]
    public class HomeController : Controller
    {
        [HttpGet]
        public IActionResult Index()
        {
            return NotFound("Chức năng Cashier đã bị loại bỏ.");
        }
    }
}

