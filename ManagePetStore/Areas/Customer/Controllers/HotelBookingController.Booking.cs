using ManagePetStore.Areas.Customer.Models;
using Microsoft.AspNetCore.Mvc;

namespace ManagePetStore.Areas.Customer.Controllers;

public partial class HotelBookingController
{
    // [nam] Trả về danh sách chuồng còn trống theo loại chuồng và khoảng thời gian khách chọn.
    [HttpGet]
    public async Task<IActionResult> AvailableCages(
        int roomTypeId,
        DateTime checkInDate,
        DateTime checkOutDate)
    {
        // [nam][Validate] Chặn request sửa tay trước khi truy vấn; sai số một phút tránh từ chối do độ trễ mạng.
        if (roomTypeId <= 0 || checkOutDate <= checkInDate || checkInDate < DateTime.Now.AddMinutes(-1))
        {
            return BadRequest(new
            {
                success = false,
                message = "Khoảng thời gian hoặc loại phòng không hợp lệ."
            });
        }

        var result = await _availabilityService.GetBookableCagesAsync(
            roomTypeId,
            checkInDate,
            checkOutDate);
        if (!result.Success)
        {
            return BadRequest(new { success = false, message = result.Message });
        }

        return Json(new
        {
            success = true,
            cages = result.Cages.Select(cage => new { cageId = cage.CageId })
        });
    }

    // [nam] Tiếp nhận yêu cầu đặt chuồng và chuyển toàn bộ xử lý nghiệp vụ sang Hotel booking service.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Book([FromForm] HotelBookingRequest request)
    {
        // [nam][Validate] DataAnnotations và IValidatableObject là lớp kiểm tra server bắt buộc, không tin dữ liệu JS.
        if (!ModelState.IsValid)
        {
            return BookingError(GetModelStateErrorMessage());
        }

        // [nam][Validate] CustomerId luôn lấy từ phiên đăng nhập, không nhận từ form để tránh giả mạo chủ booking.
        var customer = await GetCurrentCustomerAsync();
        if (customer == null)
        {
            return RedirectToAction("Login", "Account", new { area = "Customer" });
        }

        var result = await _bookingService.CreateAsync(request, customer.CustomerId);
        if (!result.Success)
        {
            return BookingError(result.Message);
        }

        TempData["SuccessMessage"] = result.Message;
        return RedirectToAction(nameof(Index));
    }

    // [nam] Trả lỗi đặt chuồng về đúng giao diện gọi action, hỗ trợ cả AJAX và form thông thường.
    private IActionResult BookingError(string message)
    {
        TempData["HotelError"] = message;
        return RedirectToAction("Index", "Home", new { area = "", hotel = "book" });
    }

    // [nam] Lấy thông báo validation đầu tiên để hiển thị cho khách hàng.
    private string GetModelStateErrorMessage()
    {
        return ModelState.Values
            .SelectMany(value => value.Errors)
            .Select(error => error.ErrorMessage)
            .FirstOrDefault(message => !string.IsNullOrWhiteSpace(message))
            ?? "Thông tin đặt phòng không hợp lệ.";
    }
}
