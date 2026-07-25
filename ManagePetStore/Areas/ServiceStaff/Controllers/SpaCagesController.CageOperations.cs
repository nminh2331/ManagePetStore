using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using ManagePetStore.Hubs;
using ManagePetStore.Models;
using ManagePetStore.Areas.ServiceStaff.Models;
using ManagePetStore.Services;
using ManagePetStore.Services.Warehouse;
using CustomerEntity = ManagePetStore.Models.Customer;

namespace ManagePetStore.Areas.ServiceStaff.Controllers
{
    public partial class SpaCagesController
    {
        [HttpPost("UpdateCageOperationalStatus")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateCageOperationalStatus(string cageId, string status, string? reason, string? note)
        {
            if (string.IsNullOrWhiteSpace(cageId) || string.IsNullOrWhiteSpace(status))
            {
                return Json(new { success = false, message = "Thông tin trạng thái chuồng không hợp lệ." });
            }

            cageId = cageId.Trim().ToUpperInvariant();
            status = status.Trim();
            reason = reason?.Trim();
            note = note?.Trim();

            if (!EditableCageStatuses.Contains(status))
            {
                return Json(new { success = false, message = "Chỉ được cập nhật Trống, Đang dọn dẹp, Bảo trì hoặc Khóa." });
            }

            bool isMaintenanceStatus = MaintenanceCageStatuses.Contains(status);
            if (isMaintenanceStatus && string.IsNullOrWhiteSpace(reason))
            {
                return Json(new { success = false, message = "Vui lòng nhập lý do trước khi đưa chuồng vào dọn dẹp, bảo trì hoặc khóa." });
            }

            if (isMaintenanceStatus && reason!.Length < 5)
            {
                return Json(new { success = false, message = "Lý do cần có ít nhất 5 ký tự để đủ rõ ràng cho lịch sử bảo trì." });
            }

            if (!string.IsNullOrWhiteSpace(reason) && reason.Length > 500)
            {
                return Json(new { success = false, message = "Lý do không được vượt quá 500 ký tự." });
            }

            if (!string.IsNullOrWhiteSpace(note) && note.Length > 1000)
            {
                return Json(new { success = false, message = "Ghi chú không được vượt quá 1000 ký tự." });
            }

            await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            try
            {
                var cage = await _context.Cages
                    .Include(c => c.RoomType)
                    .FirstOrDefaultAsync(c => c.CageId == cageId);
                if (cage == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy chuồng." });
                }

                bool hasActivePet = await _context.HotelBookings.AnyAsync(b =>
                    b.CageId == cageId && ActiveHotelStatuses.Contains(b.Status));
                if (hasActivePet)
                {
                    return Json(new { success = false, message = "Không thể đổi trạng thái vận hành khi chuồng đang có thú cưng." });
                }

                if (status == "Trống" && !cage.RoomType.Status)
                {
                    return Json(new { success = false, message = "Không thể mở lại chuồng khi loại chuồng đang ngừng hoạt động." });
                }

                bool hasUpcomingReservation = status != "Trống" && await _context.HotelBookings.AnyAsync(b =>
                    b.CageId == cageId &&
                    b.Status == "Đã đặt" &&
                    (!b.CheckOutDate.HasValue || b.CheckOutDate.Value >= DateTime.Now));
                if (hasUpcomingReservation)
                {
                    return Json(new
                    {
                        success = false,
                        message = "Chuồng đang có lịch đặt online sắp tới. Hãy xử lý lịch đặt trước khi đưa chuồng vào dọn dẹp, bảo trì hoặc khóa."
                    });
                }

                var openMaintenanceLog = await _context.RoomMaintenanceLogs
                    .Where(log => log.CageId == cageId && log.EndedAt == null)
                    .OrderByDescending(log => log.StartedAt)
                    .FirstOrDefaultAsync();

                var actor = GetCurrentStaffSnapshot();
                DateTime now = DateTime.Now;

                if (status == "Trống")
                {
                    if (openMaintenanceLog != null)
                    {
                        openMaintenanceLog.EndedAt = now;
                        openMaintenanceLog.EndedByUserId = actor.UserId;
                        openMaintenanceLog.EndedByName = actor.Name;
                        if (!string.IsNullOrWhiteSpace(note))
                        {
                            openMaintenanceLog.Note = note;
                        }
                    }

                    cage.Status = status;
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();
                    return Json(new { success = true, message = $"Đã mở lại chuồng {cageId} và ghi nhận thời gian kết thúc bảo trì/khóa nếu có." });
                }

                if (openMaintenanceLog != null)
                {
                    openMaintenanceLog.EndedAt = now;
                    openMaintenanceLog.EndedByUserId = actor.UserId;
                    openMaintenanceLog.EndedByName = actor.Name;
                    if (!string.IsNullOrWhiteSpace(note))
                    {
                        openMaintenanceLog.Note = note;
                    }

                    await _context.SaveChangesAsync();
                    openMaintenanceLog = null;
                }

                _context.RoomMaintenanceLogs.Add(new RoomMaintenanceLog
                {
                    CageId = cage.CageId,
                    PreviousStatus = cage.Status,
                    NewStatus = status,
                    Reason = reason!,
                    Note = string.IsNullOrWhiteSpace(note) ? null : note,
                    StartedAt = now,
                    CreatedByUserId = actor.UserId,
                    CreatedByName = actor.Name
                });

                cage.Status = status;
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                return Json(new { success = true, message = $"Đã cập nhật chuồng {cageId} sang trạng thái {status} và ghi nhận lịch sử bảo trì." });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Không thể cập nhật trạng thái vận hành cho chuồng {CageId}.", cageId);
                return Json(new { success = false, message = "Không thể cập nhật trạng thái chuồng do lỗi hệ thống." });
            }
        }

    }
}
