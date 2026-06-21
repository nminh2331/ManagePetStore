using Microsoft.AspNetCore.Mvc;

namespace ManagePetStore.Controllers;

[Route("Customer/Support/{action=Index}/{id?}")]
public class CustomerSupportController : Controller
{
    [HttpGet]
    public IActionResult Shipping() => View();

    [HttpGet]
    public IActionResult Warranty() => View();

    [HttpGet]
    public IActionResult Faq() => View();
}




