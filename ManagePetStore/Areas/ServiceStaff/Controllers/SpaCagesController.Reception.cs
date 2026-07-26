using ManagePetStore.Areas.ServiceStaff.Models;
using Microsoft.AspNetCore.Mvc;

namespace ManagePetStore.Areas.ServiceStaff.Controllers;

public partial class SpaCagesController
{
    // [nam] Trả về danh sách chuồng đang sẵn sàng để Staff tiếp nhận pet.
    [HttpGet("GetAvailableCages")]
    public async Task<IActionResult> GetAvailableCages(int roomTypeId)
    {
        var cages = await _hotelAvailabilityService.GetOperationallyEmptyCagesAsync(roomTypeId);
        return Json(cages.Select(cage => new
        {
            cageId = cage.CageId,
            status = cage.Status
        }));
    }

    // [nam] Điều phối luồng kiểm tra sức khỏe, tiếp nhận hoặc từ chối pet vào chuồng.
    [HttpPost("CheckIn")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CheckIn([FromForm] HotelCheckInRequest request)
    {
        if (!ModelState.IsValid)
        {
            return HotelValidationError(GetModelStateErrorMessage());
        }

        var staff = GetCurrentStaffSnapshot();
        var result = request.HealthStatus == HotelCheckInRequest.RejectedStatus
            ? await _hotelReceptionService.RejectAsync(request, staff.UserId, staff.Name)
            : await _hotelReceptionService.CheckInAsync(request, staff.UserId, staff.Name);

        TempData[result.Success ? "HotelSuccess" : "HotelError"] = result.Message;
        return RedirectToAction(nameof(Reception));
    }

    // [nam] Đưa lỗi validation tiếp nhận về màn hình Reception.
    private IActionResult HotelValidationError(string message)
    {
        TempData["HotelError"] = message;
        return RedirectToAction(nameof(Reception));
    }

    // [nam] Lấy thông báo validation đầu tiên của form tiếp nhận.
    private string GetModelStateErrorMessage()
    {
        var errors = ModelState.Values
            .SelectMany(value => value.Errors)
            .Select(error => error.ErrorMessage)
            .Where(message => !string.IsNullOrWhiteSpace(message))
            .Distinct()
            .Take(4)
            .ToList();

        return errors.Count == 0
            ? "Thông tin tiếp nhận không hợp lệ."
            : string.Join(" ", errors);
    }
}
