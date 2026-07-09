using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ManagePetStore.Areas.Cashier.Controllers
{
    [Area("Cashier")]
    [Authorize(Roles = "cashier")]
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            return RedirectToAction("Create", "Order");
        }
    }
}
