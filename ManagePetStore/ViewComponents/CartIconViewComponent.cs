using ManagePetStore.Services.Customer;
using Microsoft.AspNetCore.Mvc;

namespace ManagePetStore.ViewComponents;

public class CartIconViewComponent : ViewComponent
{
    private readonly ICartService _cartService;

    public CartIconViewComponent(ICartService cartService)
    {
        _cartService = cartService;
    }

    public IViewComponentResult Invoke()
    {
        return View(_cartService.GetTotalQuantity());
    }
}

