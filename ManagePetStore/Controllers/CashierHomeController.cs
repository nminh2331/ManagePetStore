using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ManagePetStore.Controllers
{
    [Authorize(Roles = "cashier")]
    [Route("Cashier/Home/{action=Index}/{id?}")]
    public class CashierHomeController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
