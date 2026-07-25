using System.Data;
using System.Security.Claims;
using ManagePetStore.Areas.Customer.Models;
using ManagePetStore.Models;
using ManagePetStore.Services;
using ManagePetStore.Services.Warehouse;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ManagePetStore.Areas.Customer.Controllers;

public partial class HotelBookingController
{
    [HttpPost]
    [ValidateAntiForgeryToken]
    // [nam] Huỷ booking còn hợp lệ và hoàn lại phần tồn kho thức ăn đã giữ.
    public async Task<IActionResult> Cancel(int id, string? searchTerm, string statusFilter = "all", int page = 1)
    {
        var customer = await GetCurrentCustomerAsync();
        if (customer == null)
        {
            return RedirectToAction("Login", "Account", new { area = "Customer" });
        }

        var booking = await _context.HotelBookings
            .Include(b => b.Pet)
            .Include(b => b.FoodPlan)
            .FirstOrDefaultAsync(b =>
                b.HotelBookingId == id &&
                b.CustomerId == customer.CustomerId);

        if (booking == null)
        {
            TempData["ErrorMessage"] = "Không tìm thấy lịch đặt phòng hoặc bạn không có quyền hủy.";
            return RedirectToAction(nameof(Index), new { searchTerm, statusFilter, page });
        }

        if (!string.Equals(booking.Status, "Đã đặt", StringComparison.OrdinalIgnoreCase))
        {
            TempData["ErrorMessage"] = "Chỉ có thể hủy lịch đang ở trạng thái Đã đặt.";
            return RedirectToAction(nameof(Index), new { searchTerm, statusFilter, page });
        }

        var scheduledCheckIn = booking.ScheduledCheckInDate ?? booking.CheckInDate;
        if (scheduledCheckIn <= DateTime.Now.AddHours(1))
        {
            TempData["ErrorMessage"] = "Chỉ có thể hủy online trước giờ nhận phòng ít nhất 1 giờ. Vui lòng liên hệ cửa hàng để được hỗ trợ.";
            return RedirectToAction(nameof(Index), new { searchTerm, statusFilter, page });
        }

        await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        try
        {
            if (booking.FoodPlan?.ProductSku != null && booking.FoodPlan.InventoryQuantityDeducted > 0)
            {
                var systemStockDetails = new List<StockMovementDetail>
                {
                    new StockMovementDetail
                    {
                        ProductSku = booking.FoodPlan.ProductSku,
                        Quantity = booking.FoodPlan.InventoryQuantityDeducted,
                        CostPrice = 0
                    }
                };
                await _stockMovementService.CreateSystemMovement(
                    systemUserId: 1,
                    type: "Nhập kho (Hủy đơn)",
                    status: "Chờ kiểm hàng",
                    supplier: $"Hủy lưu trú {booking.HotelBookingId}",
                    totalValue: 0,
                    details: systemStockDetails
                );
                booking.FoodPlan.InventoryQuantityDeducted = 0;
            }

            booking.Status = "Đã hủy";
            _context.PetBioTimelines.Add(new PetBioTimeline
            {
                PetId = booking.PetId,
                HotelBookingId = booking.HotelBookingId,
                Date = DateTime.Now,
                Title = "Hủy lịch lưu trú",
                Type = "HotelBookingCancelled",
                Description = "Khách hàng đã hủy lịch đặt phòng qua hệ thống; suất ăn đã giữ được hoàn lại kho."
            });
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch (ManagePetStore.Exceptions.ServiceException ex)
        {
            await transaction.RollbackAsync();
            TempData["ErrorMessage"] = ex.Message;
            return RedirectToAction(nameof(Index), new { searchTerm, statusFilter, page });
        }

        TempData["SuccessMessage"] = $"Đã hủy lịch đặt chuồng của {booking.Pet.Name}.";
        return RedirectToAction(nameof(Index), new { searchTerm, statusFilter, page });
    }

}
