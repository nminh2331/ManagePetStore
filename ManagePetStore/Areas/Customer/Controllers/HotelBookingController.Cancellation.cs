using Microsoft.AspNetCore.Mvc;

namespace ManagePetStore.Areas.Customer.Controllers;

public partial class HotelBookingController
{
    // [nam] Hủy booking chuồng của đúng khách hàng và giải phóng các tài nguyên đã giữ.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(
        int id,
        string? searchTerm,
        string statusFilter = "all",
        int page = 1)
    {
        var customer = await GetCurrentCustomerAsync();
        if (customer == null)
        {
            return RedirectToAction("Login", "Account", new { area = "Customer" });
        }

        var result = await _bookingService.CancelAsync(id, customer.CustomerId);
        TempData[result.Success ? "SuccessMessage" : "ErrorMessage"] = result.Message;
        return RedirectToAction(nameof(Index), new { searchTerm, statusFilter, page });
    }
}
