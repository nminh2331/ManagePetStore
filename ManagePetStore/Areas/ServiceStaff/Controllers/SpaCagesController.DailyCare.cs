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
        // [nam] Hiển thị danh sách pet để Staff truy cập nhật ký chăm sóc riêng của từng pet.
        [HttpGet("PetDaily")]
        public async Task<IActionResult> PetDaily(string? searchTerm)
        {
            var normalizedSearch = searchTerm?.Trim() ?? string.Empty;
            var petIdSearch = int.TryParse(normalizedSearch.TrimStart('#'), out var parsedPetId)
                ? parsedPetId
                : (int?)null;
            var query = _context.HotelBookings
                .AsNoTracking()
                .Where(booking => ActiveHotelStatuses.Contains(booking.Status));

            if (!string.IsNullOrWhiteSpace(normalizedSearch))
            {
                query = query.Where(booking =>
                    booking.Pet.Name.Contains(normalizedSearch) ||
                    booking.Pet.Species.Contains(normalizedSearch) ||
                    booking.Customer.FullName.Contains(normalizedSearch) ||
                    booking.Customer.Phone.Contains(normalizedSearch) ||
                    booking.CageId.Contains(normalizedSearch) ||
                    (petIdSearch.HasValue && booking.PetId == petIdSearch.Value));
            }

            var pets = await query
                .OrderBy(booking => booking.Pet.Name)
                .ThenBy(booking => booking.PetId)
                .Select(booking => new PetDailyCarePetRowViewModel
                {
                    PetId = booking.PetId,
                    PetName = booking.Pet.Name,
                    Species = booking.Pet.Species,
                    Breed = booking.Pet.Breed,
                    ImageUrl = booking.Pet.ImageUrl,
                    CustomerName = booking.Customer.FullName,
                    CustomerPhone = booking.Customer.Phone,
                    HotelBookingId = booking.HotelBookingId,
                    CageId = booking.CageId,
                    RoomTypeCode = booking.Cage.RoomType.Code,
                    RoomTypeName = booking.Cage.RoomType.Type,
                    CheckInAt = booking.ActualCheckInAt ?? booking.CheckInDate,
                    ExpectedCheckOutAt = booking.ScheduledCheckOutDate ?? booking.CheckOutDate,
                    CareLogCount = booking.FoodDiaryLogs.Count,
                    LastCareAt = booking.FoodDiaryLogs
                        .OrderByDescending(log => log.OccurredAt)
                        .Select(log => log.OccurredAt)
                        .FirstOrDefault()
                })
                .ToListAsync();

            return View(
                "~/Areas/ServiceStaff/Views/SpaServices/PetDaily.cshtml",
                new PetDailyCareListViewModel
                {
                    SearchTerm = normalizedSearch,
                    Pets = pets
                });
        }

        // [nam] Hiển thị nhật ký hiện tại và lịch sử lưu trú của một pet xác định bằng PetId.
        [HttpGet("PetDaily/{petId:int}")]
        public async Task<IActionResult> PetDailyDetails(int petId, string tab = "current")
        {
            var pet = await _context.Pets
                .AsNoTracking()
                .Include(item => item.Customer)
                .FirstOrDefaultAsync(item => item.PetId == petId);
            if (pet == null)
            {
                return NotFound();
            }

            var bookings = await _context.HotelBookings
                .AsNoTracking()
                .Include(booking => booking.Cage)
                    .ThenInclude(cage => cage.RoomType)
                .Include(booking => booking.FoodPlan)
                .Where(booking => booking.PetId == petId)
                .OrderByDescending(booking => booking.HotelBookingId)
                .ToListAsync();

            var activeBooking = bookings.FirstOrDefault(booking => ActiveHotelStatuses.Contains(booking.Status));
            var logs = await _context.FoodDiaryLogs
                .AsNoTracking()
                .Where(log => log.HotelBookingId.HasValue &&
                              log.HotelBooking != null &&
                              log.HotelBooking.PetId == petId)
                .OrderByDescending(log => log.OccurredAt ?? log.HotelBooking!.CheckInDate)
                .ThenByDescending(log => log.LogId)
                .Take(500)
                .Select(log => new PetDailyCareLogViewModel
                {
                    LogId = log.LogId,
                    HotelBookingId = log.HotelBookingId!.Value,
                    OccurredAt = log.OccurredAt,
                    LegacyTime = log.Time,
                    ActivityType = log.ActivityType,
                    Title = log.Title,
                    Status = log.Status,
                    FoodType = log.FoodType,
                    Amount = log.Amount,
                    IsExtraCharge = log.IsExtraCharge,
                    ExtraChargeAmount = log.ExtraChargeAmount,
                    StaffName = log.StaffName,
                    Note = log.Note,
                    MediaUrl = log.MediaUrl ?? log.PhotoUrl,
                    MediaType = log.MediaType,
                    IsVisibleToCustomer = log.IsVisibleToCustomer
                })
                .ToListAsync();

            var stays = bookings.Select(booking => new PetDailyCareStayViewModel
            {
                HotelBookingId = booking.HotelBookingId,
                CageId = booking.CageId,
                RoomTypeCode = booking.Cage.RoomType.Code,
                RoomTypeName = booking.Cage.RoomType.Type,
                CheckInAt = booking.ActualCheckInAt ?? booking.ScheduledCheckInDate ?? booking.CheckInDate,
                CheckOutAt = booking.ActualCheckOutAt ?? booking.ScheduledCheckOutDate ?? booking.CheckOutDate,
                Status = booking.Status,
                StatusKey = ResolveHotelStatusKey(booking.Status),
                FoodPlanName = booking.FoodPlan?.FoodNameSnapshot ?? "Chưa ghi nhận gói ăn",
                FoodProductSku = booking.FoodPlan?.ProductSku,
                PortionGrams = booking.FoodPlan?.PortionGrams ?? 0,
                MealsPerDay = booking.FoodPlan?.MealsPerDay ?? 0
            }).ToList();

            var model = new PetDailyCareDetailsViewModel
            {
                PetId = pet.PetId,
                PetName = pet.Name,
                Species = pet.Species,
                Breed = pet.Breed,
                Age = pet.Age,
                Weight = pet.Weight,
                Pathology = pet.Pathology,
                ImageUrl = pet.ImageUrl,
                CustomerName = pet.Customer.FullName,
                CustomerPhone = pet.Customer.Phone,
                CustomerEmail = pet.Customer.Email,
                SelectedTab = string.Equals(tab, "all", StringComparison.OrdinalIgnoreCase) ? "all" : "current",
                CurrentStay = activeBooking == null
                    ? null
                    : stays.First(stay => stay.HotelBookingId == activeBooking.HotelBookingId),
                Stays = stays,
                CurrentLogs = activeBooking == null
                    ? []
                    : logs.Where(log => log.HotelBookingId == activeBooking.HotelBookingId).ToList(),
                AllLogs = logs
            };

            return View("~/Areas/ServiceStaff/Views/SpaServices/PetDailyDetails.cshtml", model);
        }


        // [nam] Ghi nhật ký chăm sóc, media và chi phí phát sinh cho pet đang lưu trú.
        [HttpPost("HotelCareLog")]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(55 * 1024 * 1024)]
        public async Task<IActionResult> CreateHotelCareLog(HotelCareLogRequest request)
        {
            var allowedActivityTypes = new[]
            {
                "General", "Feeding", "Health", "Exercise", "Hygiene", "Medication", "CameraSnapshot"
            };

            if (!allowedActivityTypes.Contains(request.ActivityType, StringComparer.OrdinalIgnoreCase))
            {
                ModelState.AddModelError(nameof(request.ActivityType), "Loại hoạt động không hợp lệ.");
            }

            bool isFeeding = string.Equals(request.ActivityType, "Feeding", StringComparison.OrdinalIgnoreCase);
            if (!isFeeding)
            {
                request.FoodType = null;
                request.ServedGrams = null;
                request.IsExtraCharge = false;
                request.ExtraChargeAmount = 0;
            }

            if (request.IsExtraCharge && request.ExtraChargeAmount <= 0)
            {
                ModelState.AddModelError(nameof(request.ExtraChargeAmount), "Phụ phí bữa ăn phải lớn hơn 0.");
            }

            var booking = await _context.HotelBookings
                .Include(item => item.Pet)
                .Include(item => item.Customer)
                .Include(item => item.FoodPlan)
                .FirstOrDefaultAsync(item => item.HotelBookingId == request.HotelBookingId);

            if (booking == null)
            {
                return NotFound();
            }

            if (!string.Equals(booking.Status, "Active", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(booking.Status, "Đang ở", StringComparison.OrdinalIgnoreCase))
            {
                ModelState.AddModelError(string.Empty, "Chỉ có thể cập nhật nhật ký cho booking đang lưu trú.");
            }

            var occurredAt = request.OccurredAt ?? DateTime.Now;
            var earliestAllowed = (booking.ActualCheckInAt ?? booking.CheckInDate).AddHours(-1);
            if (occurredAt > DateTime.Now.AddMinutes(5) || occurredAt < earliestAllowed)
            {
                ModelState.AddModelError(nameof(request.OccurredAt), "Thời gian nhật ký phải thuộc lượt lưu trú và không được ở tương lai.");
            }

            if (!ModelState.IsValid)
            {
                TempData["HotelCareError"] = ModelState.Values
                    .SelectMany(value => value.Errors)
                    .Select(error => error.ErrorMessage)
                    .FirstOrDefault() ?? "Thông tin nhật ký không hợp lệ.";
                return RedirectAfterCareLog(request, booking);
            }

            HotelCareMediaResult? media = null;
            try
            {
                media = await _hotelCareMediaService.SaveAsync(booking.HotelBookingId, request.MediaFile);
            }
            catch (InvalidOperationException ex)
            {
                TempData["HotelCareError"] = ex.Message;
                return RedirectAfterCareLog(request, booking);
            }

            var staffUserId = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var parsedUserId)
                ? parsedUserId
                : (int?)null;
            var staffName = User.FindFirstValue("FullName") ?? User.Identity?.Name ?? "Nhân viên";
            var safeTitle = request.Title.Trim();
            var safeStatus = request.Status.Trim();
            var safeNote = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
            var notificationMessage = BuildCareNotificationMessage(safeStatus, safeNote);
            var notificationTitle = $"{booking.Pet.Name}: {safeTitle}";
            if (notificationTitle.Length > 180)
            {
                notificationTitle = notificationTitle[..177] + "...";
            }

            var careLog = new FoodDiaryLog
            {
                LogId = $"FD-{Guid.NewGuid():N}",
                PetName = booking.Pet.Name,
                CageId = booking.CageId,
                HotelBookingId = booking.HotelBookingId,
                ActivityType = request.ActivityType,
                Title = safeTitle,
                Status = safeStatus,
                FoodType = isFeeding
                    ? string.IsNullOrWhiteSpace(request.FoodType)
                        ? booking.FoodPlan?.FoodNameSnapshot ?? "Không áp dụng"
                        : request.FoodType.Trim()
                    : "Không áp dụng",
                Amount = isFeeding && request.ServedGrams.HasValue
                    ? $"{request.ServedGrams:0.##} g"
                    : "Không áp dụng",
                PhotoUrl = media?.MediaType == "Image" ? media.PublicUrl : null,
                MediaUrl = media?.PublicUrl,
                MediaType = media?.MediaType,
                Note = safeNote,
                Time = occurredAt.ToString("HH:mm"),
                OccurredAt = occurredAt,
                StaffName = staffName,
                IsVisibleToCustomer = request.IsVisibleToCustomer,
                CreatedByUserId = staffUserId,
                FoodPlanId = isFeeding ? booking.FoodPlan?.FoodPlanId : null,
                MealType = null,
                ServedGrams = isFeeding ? request.ServedGrams : null,
                ConsumedPercent = null,
                IsExtraCharge = isFeeding && request.IsExtraCharge,
                ExtraChargeAmount = isFeeding && request.IsExtraCharge ? request.ExtraChargeAmount : 0
            };

            _context.FoodDiaryLogs.Add(careLog);
            CustomerNotification? notification = null;
            if (request.IsVisibleToCustomer)
            {
                notification = new CustomerNotification
                {
                    CustomerId = booking.CustomerId,
                    HotelBookingId = booking.HotelBookingId,
                    Type = "DailyCare",
                    Title = notificationTitle,
                    Message = notificationMessage,
                    LinkUrl = $"/Customer/HotelBooking/Details/{booking.HotelBookingId}",
                    CreatedAt = DateTime.Now
                };
                _context.CustomerNotifications.Add(notification);
            }

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                await _hotelCareMediaService.DeleteAsync(media?.PublicUrl);
                _logger.LogError(ex, "Cannot save daily care log for hotel booking {HotelBookingId}.", booking.HotelBookingId);
                TempData["HotelCareError"] = "Không thể lưu nhật ký lúc này. Vui lòng thử lại.";
                return RedirectAfterCareLog(request, booking);
            }

            if (notification != null)
            {
                try
                {
                    await _hotelCareHub.Clients
                        .Group(HotelCareHub.GroupName(booking.CustomerId))
                        .SendAsync("CareLogUpdated", new
                        {
                            notificationId = notification.NotificationId,
                            bookingId = booking.HotelBookingId,
                            petName = booking.Pet.Name,
                            title = notification.Title,
                            message = notification.Message,
                            mediaUrl = media?.PublicUrl,
                            mediaType = media?.MediaType,
                            occurredAt,
                            linkUrl = notification.LinkUrl
                        });
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Daily care log was saved but realtime delivery failed for customer {CustomerId}.", booking.CustomerId);
                }

                await _hotelEmailService.SendCareLogAsync(
                    booking.Customer.Email,
                    booking.Customer.FullName,
                    booking.HotelBookingId,
                    booking.Pet.Name,
                    notification.Title,
                    notification.Message,
                    occurredAt);
            }

            TempData["HotelCareSuccess"] = $"Đã cập nhật nhật ký chăm sóc của {booking.Pet.Name}.";
            return RedirectAfterCareLog(request, booking);
        }

        // [nam] Chuyển Staff về đúng màn hình sau khi cập nhật nhật ký chăm sóc.
        private IActionResult RedirectAfterCareLog(HotelCareLogRequest request, HotelBooking booking)
        {
            return request.ReturnToPetDaily
                ? RedirectToAction(nameof(PetDailyDetails), new { petId = booking.PetId, tab = "current" })
                : RedirectToAction(nameof(HotelHistoryDetails), new { id = booking.HotelBookingId });
        }

        // [nam] Tạo nội dung thông báo chăm sóc gửi tới chủ pet.
        private static string BuildCareNotificationMessage(string status, string? note)
        {
            var message = string.IsNullOrWhiteSpace(note) ? status : $"{status}. {note}";
            return message.Length > 500 ? message[..497] + "..." : message;
        }

    }
}
