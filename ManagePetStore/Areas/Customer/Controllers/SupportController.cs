using Microsoft.AspNetCore.Mvc;

namespace ManagePetStore.Areas.Customer.Controllers;

[Area("Customer")]
public class SupportController : Controller
{
    [HttpGet]
    public IActionResult Shipping() => View();

    [HttpGet]
    public IActionResult Warranty() => View();

    [HttpGet]
    public IActionResult Faq() => View();
}
