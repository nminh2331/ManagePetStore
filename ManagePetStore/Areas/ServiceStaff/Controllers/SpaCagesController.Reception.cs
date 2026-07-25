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
        [HttpGet("GetAvailableCages")]
        public async Task<IActionResult> GetAvailableCages(int roomTypeId)
        {
            var cages = await _context.Cages
                .Where(c => c.RoomTypeId == roomTypeId &&
                            c.Status == "Trống" &&
                            c.RoomType.Status &&
                            HotelRoomTypeCatalog.Codes.Contains(c.RoomType.Code))
                .Select(c => new { cageId = c.CageId, status = c.Status })
                .ToListAsync();
            return Json(cages);
        }

        [HttpPost("CheckIn")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CheckIn([FromForm] HotelCheckInRequest request)
        {
            if (!ModelState.IsValid)
            {
                return HotelValidationError(GetModelStateErrorMessage());
            }

            if (request.HealthStatus == HotelCheckInRequest.RejectedStatus)
            {
                return await RejectHotelReceptionAsync(request);
            }

            string customerPhone = new(request.CustomerPhone.Where(char.IsDigit).ToArray());
            string cageId = request.CageId.Trim().ToUpperInvariant();
            string healthNote = request.HealthNote?.Trim() ?? string.Empty;
            DateTime checkInDate = request.CheckInDate!.Value;
            DateTime? checkOutDate = request.CheckOutDate;

            await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            try
            {
                var cage = await _context.Cages
                    .Include(c => c.RoomType)
                    .FirstOrDefaultAsync(c => c.CageId == cageId);

                if (cage == null)
                {
                    return HotelValidationError("Không tìm thấy chuồng đã chọn.");
                }

                if (cage.Status != "Trống")
                {
                    return HotelValidationError($"Chuồng {cageId} hiện không còn trống.");
                }

                if (cage.RoomType == null || !cage.RoomType.Status || cage.RoomTypeId != request.RoomTypeId)
                {
                    return HotelValidationError("Chuồng đã chọn không thuộc loại chuồng đang hoạt động.");
                }

                var medicalRecord = await _context.MedicalRecords
                    .Include(record => record.Pet)
                        .ThenInclude(pet => pet.Customer)
                    .FirstOrDefaultAsync(record => record.RecordId == request.MedicalRecordId!.Value);
                if (medicalRecord == null)
                {
                    return HotelValidationError("Không tìm thấy sổ y tế đã chọn.");
                }

                if (medicalRecord.HotelBookingId.HasValue)
                {
                    return HotelValidationError("Sổ y tế này đã được sử dụng cho một lượt lưu trú khác.");
                }

                if (medicalRecord.Weight <= 0)
                {
                    return HotelValidationError(
                        "Sổ y tế chưa có cân nặng hợp lệ. Vui lòng cập nhật sổ trước khi tiếp nhận vào chuồng.");
                }

                var pet = medicalRecord.Pet;
                var customer = pet.Customer;
                string storedCustomerPhone = new((customer.Phone ?? string.Empty).Where(char.IsDigit).ToArray());
                if (!string.Equals(storedCustomerPhone, customerPhone, StringComparison.Ordinal))
                {
                    return HotelValidationError("Số điện thoại không khớp với chủ của sổ y tế đã chọn.");
                }

                if (!string.Equals(pet.Status, "Active", StringComparison.OrdinalIgnoreCase))
                {
                    return HotelValidationError("Hồ sơ thú cưng đã chọn không còn hoạt động.");
                }

                HotelBooking? onlineReservation = null;
                if (request.HotelBookingId.HasValue)
                {
                    onlineReservation = await _context.HotelBookings
                        .Include(booking => booking.FoodPlan)
                        .FirstOrDefaultAsync(booking =>
                            booking.HotelBookingId == request.HotelBookingId.Value &&
                            booking.Status == "Đã đặt");

                    if (onlineReservation == null)
                    {
                        return HotelValidationError("Không tìm thấy lịch đặt online đang chờ tiếp nhận.");
                    }

                    if (onlineReservation.PetId != pet.PetId || onlineReservation.CustomerId != customer.CustomerId)
                    {
                        return HotelValidationError("Lịch đặt online không khớp với chủ nuôi hoặc thú cưng đã chọn.");
                    }

                    if (onlineReservation.CheckInDate.Date > DateTime.Today)
                    {
                        return HotelValidationError("Chưa đến ngày nhận của lịch đặt online này.");
                    }
                }

                int excludedBookingId = onlineReservation?.HotelBookingId ?? 0;
                bool petHasBlockingBooking = await _context.HotelBookings.AnyAsync(booking =>
                    booking.PetId == pet.PetId &&
                    booking.HotelBookingId != excludedBookingId &&
                    BlockingHotelStatuses.Contains(booking.Status));
                if (petHasBlockingBooking)
                {
                    return HotelValidationError($"{pet.Name} đã có lịch đặt hoặc đang lưu trú, không thể tiếp nhận thêm.");
                }

                if (onlineReservation != null &&
                    !string.Equals(onlineReservation.CageId, cageId, StringComparison.OrdinalIgnoreCase))
                {
                    return HotelValidationError(
                        $"{pet.Name} đã đặt online chuồng {onlineReservation.CageId} trong ngày nhận này. Vui lòng chọn đúng chuồng đã giữ.");
                }

                if (onlineReservation?.CheckOutDate != null)
                {
                    checkOutDate = onlineReservation.CheckOutDate;
                }

                string foodProductSku = request.FoodProductSku.Trim();
                var foodProduct = await _context.Products
                    .AsNoTracking()
                    .Include(product => product.Category)
                    .FirstOrDefaultAsync(product =>
                        product.Sku == foodProductSku &&
                        !product.IsDeleted &&
                        product.Unit == HotelFoodCatalog.DailyUnit &&
                        product.Category != null &&
                        !product.Category.IsDeleted &&
                        product.Category.Code == HotelFoodCatalog.CategoryCode);
                if (foodProduct == null)
                {
                    return HotelValidationError("Gói thức ăn đã chọn không còn sẵn trong kho cửa hàng.");
                }

                if (!HotelFoodCatalog.IsSpeciesCompatible(foodProduct.AnimalType, pet.Species))
                {
                    return HotelValidationError("Gói thức ăn đã chọn không phù hợp với loài của thú cưng.");
                }

                if (foodProduct.Price <= 0 && (onlineReservation?.FoodPlan?.BasePricePerDaySnapshot ?? 0) <= 0)
                {
                    return HotelValidationError("Gói thức ăn chưa có giá bán hợp lệ.");
                }

                bool petHasScheduleConflict = await _context.HotelBookings.AnyAsync(b =>
                    b.PetId == pet.PetId &&
                    b.HotelBookingId != (onlineReservation != null ? onlineReservation.HotelBookingId : 0) &&
                    (b.Status == "Đã đặt" || b.Status == "Active" || b.Status == "Đang ở") &&
                    (!checkOutDate.HasValue || b.CheckInDate < checkOutDate.Value) &&
                    (!b.CheckOutDate.HasValue || b.CheckOutDate.Value > checkInDate));

                if (petHasScheduleConflict)
                {
                    return HotelValidationError($"{pet.Name} có lịch lưu trú khác trùng với khoảng thời gian tiếp nhận.");
                }

                bool cageHasScheduleConflict = await _context.HotelBookings.AnyAsync(b =>
                    b.CageId == cageId &&
                    b.HotelBookingId != (onlineReservation != null ? onlineReservation.HotelBookingId : 0) &&
                    (b.Status == "Đã đặt" || b.Status == "Active" || b.Status == "Đang ở") &&
                    (!checkOutDate.HasValue || b.CheckInDate < checkOutDate.Value) &&
                    (!b.CheckOutDate.HasValue || b.CheckOutDate.Value > checkInDate));

                if (cageHasScheduleConflict)
                {
                    return HotelValidationError($"Chuồng {cageId} đã được giữ cho một lịch lưu trú khác trong khoảng thời gian này.");
                }

                int estimatedStayDays = HotelPricingPolicy.CalculateStayDays(
                    checkInDate,
                    checkOutDate ?? checkInDate.AddDays(1));
                decimal dailyPrice = onlineReservation?.BaseDailyPrice > 0
                    ? onlineReservation.BaseDailyPrice
                    : cage.RoomType.DailyPrice;
                decimal subtotal = onlineReservation?.Subtotal > 0
                    ? onlineReservation.Subtotal
                    : dailyPrice * estimatedStayDays;
                bool keepReservedFoodSnapshot = onlineReservation?.FoodPlan?.ProductSku == foodProduct.Sku;
                decimal baseFoodPricePerDay = keepReservedFoodSnapshot &&
                    onlineReservation!.FoodPlan!.BasePricePerDaySnapshot > 0
                        ? onlineReservation.FoodPlan.BasePricePerDaySnapshot
                        : foodProduct.Price;
                var foodQuote = HotelFoodPricing.Calculate(
                    baseFoodPricePerDay,
                    medicalRecord.Weight,
                    estimatedStayDays);
                int currentBookingId = onlineReservation?.HotelBookingId ?? 0;
                int reservedFoodUnits = await _context.HotelBookingFoodPlans
                    .Where(plan => plan.ProductSku == foodProduct.Sku &&
                                   plan.HotelBookingId != currentBookingId &&
                                   plan.InventoryQuantityDeducted == 0 &&
                                   BlockingHotelStatuses.Contains(plan.HotelBooking.Status))
                    .SumAsync(plan => (int?)plan.ChargeableDays) ?? 0;
                int currentReservedFoodUnits = onlineReservation?.FoodPlan?.ProductSku == foodProduct.Sku
                    ? onlineReservation.FoodPlan.InventoryQuantityDeducted
                    : 0;
                int availableFoodUnits = Math.Max(
                    0,
                    foodProduct.Stock + currentReservedFoodUnits - reservedFoodUnits);
                if (availableFoodUnits < foodQuote.InventoryUnits)
                {
                    return HotelValidationError(
                        $"{foodProduct.Name} chỉ còn {availableFoodUnits} suất chuẩn, " +
                        $"không đủ {foodQuote.InventoryUnits} suất cho {estimatedStayDays} ngày ({foodQuote.WeightBand}).");
                }

                decimal foodPricePerDay = foodQuote.PricePerDay;
                decimal foodTotal = foodQuote.TotalAmount;

                HotelBooking hotelBooking;
                if (onlineReservation != null)
                {
                    onlineReservation.ScheduledCheckInDate ??= onlineReservation.CheckInDate;
                    onlineReservation.ScheduledCheckOutDate ??= onlineReservation.CheckOutDate;
                    onlineReservation.CheckInDate = checkInDate;
                    onlineReservation.ActualCheckInAt = checkInDate;
                    onlineReservation.FinalAmount = Math.Max(0, subtotal - onlineReservation.Discount + foodTotal);
                    onlineReservation.Status = "Đang ở";
                    hotelBooking = onlineReservation;
                }
                else
                {
                    decimal discount = HotelPricingPolicy.CalculateMembershipDiscount(
                        subtotal,
                        customer.MembershipTier);
                    hotelBooking = new HotelBooking
                    {
                        CageId = cageId,
                        PetId = pet.PetId,
                        CustomerId = customer.CustomerId,
                        CheckInDate = checkInDate,
                        CheckOutDate = checkOutDate,
                        ScheduledCheckInDate = checkInDate,
                        ScheduledCheckOutDate = checkOutDate,
                        ActualCheckInAt = checkInDate,
                        StayDays = estimatedStayDays,
                        BaseDailyPrice = dailyPrice,
                        Subtotal = subtotal,
                        Discount = discount,
                        FinalAmount = Math.Max(0, subtotal - discount + foodTotal),
                        EarnedPoints = 0,
                        Status = "Đang ở"
                    };
                    _context.HotelBookings.Add(hotelBooking);
                }

                var foodPlan = onlineReservation?.FoodPlan;
                if (foodPlan == null)
                {
                    foodPlan = new HotelBookingFoodPlan
                    {
                        HotelBooking = hotelBooking,
                        CreatedAt = DateTime.Now
                    };
                    _context.HotelBookingFoodPlans.Add(foodPlan);
                }

                string? previousFoodProductSku = foodPlan.ProductSku;
                int previousInventoryQuantity = foodPlan.InventoryQuantityDeducted;
                if (previousInventoryQuantity > 0 &&
                    !string.IsNullOrWhiteSpace(previousFoodProductSku) &&
                    !string.Equals(previousFoodProductSku, foodProduct.Sku, StringComparison.OrdinalIgnoreCase))
                {
                    await _inventoryBatchService.RestockToBatches(
                        previousFoodProductSku,
                        previousInventoryQuantity);
                    foodPlan.InventoryQuantityDeducted = 0;
                }

                foodPlan.FoodOptionId = null;
                foodPlan.ProductSku = foodProduct.Sku;
                foodPlan.PlanType = "HotelProduct";
                if (!keepReservedFoodSnapshot)
                {
                    foodPlan.FoodNameSnapshot = foodProduct.Name;
                    foodPlan.ProductUnitSnapshot = foodProduct.Unit;
                }
                foodPlan.BasePricePerDaySnapshot = foodQuote.BasePricePerDay;
                foodPlan.PetWeightSnapshot = foodQuote.PetWeightKg;
                foodPlan.PortionMultiplierSnapshot = foodQuote.PortionMultiplier;
                foodPlan.PricePerDaySnapshot = foodPricePerDay;
                foodPlan.PortionGrams = 0;
                foodPlan.MealsPerDay = 0;
                foodPlan.ChargeableDays = estimatedStayDays;
                foodPlan.TotalAmount = foodTotal;

                int inventoryToDeduct = foodQuote.InventoryUnits - foodPlan.InventoryQuantityDeducted;
                if (inventoryToDeduct > 0)
                {
                    await _inventoryBatchService.DeductStockFIFO(foodProduct.Sku, inventoryToDeduct);
                }
                else if (inventoryToDeduct < 0)
                {
                    await _inventoryBatchService.RestockToBatches(foodProduct.Sku, -inventoryToDeduct);
                }
                foodPlan.InventoryQuantityDeducted = foodQuote.InventoryUnits;

                _context.PetBioTimelines.Add(new PetBioTimeline
                {
                    PetId = pet.PetId,
                    HotelBooking = hotelBooking,
                    Date = DateTime.Now,
                    Title = "Kiểm tra sức khỏe đầu vào",
                    Type = "HealthCheckIn",
                    Description = BuildHealthCheckDescription(request, medicalRecord, healthNote)
                });

                _context.PetBioTimelines.Add(new PetBioTimeline
                {
                    PetId = pet.PetId,
                    HotelBooking = hotelBooking,
                    Date = DateTime.Now,
                    Title = "Tiếp nhận lưu trú",
                    Type = "PetCheckIn",
                    Description = BuildPetCheckInDescription(
                        request,
                        cageId,
                        customer.FullName,
                        foodPlan.FoodNameSnapshot,
                        checkOutDate)
                });

                var assessor = GetCurrentStaffSnapshot();
                _context.HotelCheckInAssessments.Add(new HotelCheckInAssessment
                {
                    HotelBooking = hotelBooking,
                    MedicalRecord = medicalRecord,
                    Decision = request.HealthStatus,
                    Note = string.IsNullOrWhiteSpace(healthNote) ? null : healthNote,
                    AssessedByUserId = assessor.UserId,
                    AssessedByName = assessor.Name,
                    AssessedAt = DateTime.Now
                });

                medicalRecord.HotelBooking = hotelBooking;

                _context.HotelCageStaySegments.Add(new HotelCageStaySegment
                {
                    HotelBooking = hotelBooking,
                    CageId = cage.CageId,
                    RoomTypeId = cage.RoomTypeId,
                    DailyPriceSnapshot = dailyPrice,
                    StartedAt = checkInDate,
                    StartReason = "CheckIn",
                    CreatedAt = DateTime.Now
                });

                cage.Status = "Đang dùng";

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                await _hotelEmailService.SendCheckInAsync(
                    customer.Email,
                    customer.FullName,
                    hotelBooking.HotelBookingId,
                    pet.Name,
                    cageId,
                    checkInDate,
                    checkOutDate);

                TempData["HotelSuccess"] = $"Đã hoàn tất tiếp nhận lưu trú cho {pet.Name} tại chuồng {cageId}!";
            }
            catch (ManagePetStore.Exceptions.ServiceException ex)
            {
                await transaction.RollbackAsync();
                return HotelValidationError(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                await transaction.RollbackAsync();
                return HotelValidationError(ex.Message);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Không thể kiểm tra sức khỏe và tiếp nhận thú cưng vào chuồng {CageId}", cageId);
                TempData["HotelError"] = "Không thể tiếp nhận thú cưng do lỗi hệ thống. Vui lòng thử lại.";
            }

            return RedirectToAction(nameof(Reception));
        }

        private async Task<IActionResult> RejectHotelReceptionAsync(HotelCheckInRequest request)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            try
            {
                var booking = await _context.HotelBookings
                    .Include(item => item.Pet)
                        .ThenInclude(pet => pet.Customer)
                    .Include(item => item.FoodPlan)
                    .Include(item => item.CheckInAssessment)
                    .FirstOrDefaultAsync(item =>
                        item.HotelBookingId == request.HotelBookingId!.Value &&
                        item.Status == "Đã đặt");
                if (booking == null)
                {
                    return HotelValidationError("Booking không còn ở trạng thái chờ tiếp nhận.");
                }

                if (booking.CheckInDate.Date > DateTime.Today)
                {
                    return HotelValidationError("Chưa đến ngày nhận của booking này.");
                }

                if (booking.CheckInAssessment != null)
                {
                    return HotelValidationError("Booking này đã có kết luận kiểm tra sức khỏe.");
                }

                var medicalRecord = await _context.MedicalRecords
                    .FirstOrDefaultAsync(record =>
                        record.RecordId == request.MedicalRecordId!.Value &&
                        record.PetId == booking.PetId &&
                        record.HotelBookingId == null &&
                        record.Weight > 0);
                if (medicalRecord == null)
                {
                    return HotelValidationError("Sổ y tế không còn khả dụng hoặc không thuộc pet trong booking.");
                }

                string submittedPhone = new(request.CustomerPhone.Where(char.IsDigit).ToArray());
                string storedPhone = new((booking.Pet.Customer.Phone ?? string.Empty).Where(char.IsDigit).ToArray());
                if (!string.Equals(submittedPhone, storedPhone, StringComparison.Ordinal))
                {
                    return HotelValidationError("Số điện thoại không khớp với chủ của pet trong booking.");
                }

                if (booking.FoodPlan?.ProductSku != null && booking.FoodPlan.InventoryQuantityDeducted > 0)
                {
                    await _inventoryBatchService.RestockToBatches(
                        booking.FoodPlan.ProductSku,
                        booking.FoodPlan.InventoryQuantityDeducted);
                    booking.FoodPlan.InventoryQuantityDeducted = 0;
                }

                string rejectionNote = request.HealthNote!.Trim();
                var assessor = GetCurrentStaffSnapshot();
                _context.HotelCheckInAssessments.Add(new HotelCheckInAssessment
                {
                    HotelBooking = booking,
                    MedicalRecord = medicalRecord,
                    Decision = HotelCheckInRequest.RejectedStatus,
                    Note = rejectionNote,
                    AssessedByUserId = assessor.UserId,
                    AssessedByName = assessor.Name,
                    AssessedAt = DateTime.Now
                });

                booking.Status = "Từ chối tiếp nhận";
                _context.PetBioTimelines.Add(new PetBioTimeline
                {
                    PetId = booking.PetId,
                    HotelBooking = booking,
                    Date = DateTime.Now,
                    Title = "Từ chối tiếp nhận lưu trú",
                    Type = "HotelReceptionRejected",
                    Description = $"Sổ y tế #{medicalRecord.RecordId}; lý do: {rejectionNote}. " +
                        $"Người đánh giá: {assessor.Name}. Chuồng và suất ăn đã giữ được giải phóng."
                });
                _context.CustomerNotifications.Add(new CustomerNotification
                {
                    CustomerId = booking.CustomerId,
                    HotelBooking = booking,
                    Type = "HotelReceptionRejected",
                    Title = $"Không thể tiếp nhận {booking.Pet.Name}",
                    Message = $"Booking #{booking.HotelBookingId} bị từ chối tiếp nhận sau kiểm tra sức khỏe. Lý do: {rejectionNote}",
                    LinkUrl = $"/Customer/HotelBooking/Details/{booking.HotelBookingId}",
                    IsRead = false,
                    CreatedAt = DateTime.Now
                });

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                TempData["HotelSuccess"] = $"Đã lưu quyết định từ chối tiếp nhận {booking.Pet.Name} và hoàn lại tài nguyên đã giữ.";
            }
            catch (ManagePetStore.Exceptions.ServiceException ex)
            {
                await transaction.RollbackAsync();
                return HotelValidationError(ex.Message);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Không thể lưu quyết định từ chối booking Hotel {BookingId}.", request.HotelBookingId);
                return HotelValidationError("Không thể lưu quyết định từ chối do lỗi hệ thống. Vui lòng thử lại.");
            }

            return RedirectToAction(nameof(Reception));
        }

        private IActionResult HotelValidationError(string message)
        {
            TempData["HotelError"] = message;
            return RedirectToAction(nameof(Reception));
        }

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

        private string BuildHealthCheckDescription(
            HotelCheckInRequest request,
            MedicalRecord medicalRecord,
            string healthNote)
        {
            string conclusion = request.HealthStatus == HotelCheckInRequest.FitStatus
                ? "Đủ điều kiện lưu trú"
                : "Đủ điều kiện nhưng cần theo dõi";
            string checkedBy = User.FindFirst("FullName")?.Value ?? User.Identity?.Name ?? "Nhân viên dịch vụ";

            return $"Hình thức tiếp nhận: Dùng sổ y tế có sẵn\n"
                 + $"Sổ y tế: #{medicalRecord.RecordId} - khám ngày {medicalRecord.DateCreated:dd/MM/yyyy HH:mm}\n"
                 + $"Tình trạng trong sổ: {medicalRecord.HealthStatus}\n"
                 + $"Cân nặng trong sổ: {medicalRecord.Weight:0.##} kg\n"
                 + $"Triệu chứng/bệnh lý: {(string.IsNullOrWhiteSpace(medicalRecord.Symptoms) ? "Không ghi nhận" : medicalRecord.Symptoms)}\n"
                 + $"Kết luận: {conclusion}\n"
                 + $"Ghi chú tiếp nhận: {(string.IsNullOrWhiteSpace(healthNote) ? "Không có" : healthNote)}\n"
                 + $"Người kiểm tra: {checkedBy}";
        }

        private string BuildPetCheckInDescription(
            HotelCheckInRequest request,
            string cageId,
            string customerName,
            string foodPlanName,
            DateTime? checkOutDate)
        {
            string checkedBy = User.FindFirst("FullName")?.Value ?? User.Identity?.Name ?? "Nhân viên dịch vụ";
            string expectedCheckout = checkOutDate.HasValue
                ? checkOutDate.Value.TimeOfDay == TimeSpan.Zero
                    ? checkOutDate.Value.ToString("dd/MM/yyyy")
                    : checkOutDate.Value.ToString("dd/MM/yyyy HH:mm")
                : "Chưa xác định";

            return $"Hình thức tiếp nhận: Dùng sổ y tế có sẵn\n"
                 + $"Chuồng tiếp nhận: {cageId}\n"
                 + $"Chủ thú cưng: {customerName}\n"
                 + $"Kế hoạch ăn: {foodPlanName}\n"
                 + $"Ngày nhận: {request.CheckInDate!.Value:dd/MM/yyyy HH:mm}\n"
                 + $"Ngày trả dự kiến: {expectedCheckout}\n"
                 + $"Nhân viên tiếp nhận: {checkedBy}";
        }

    }
}
