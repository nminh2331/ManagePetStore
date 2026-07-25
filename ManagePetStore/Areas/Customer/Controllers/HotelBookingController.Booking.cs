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
    [HttpGet]
    // [nam] Trả về danh sách chuồng trống theo loại phòng và khoảng thời gian đã chọn.
    public async Task<IActionResult> AvailableCages(int roomTypeId, DateTime checkInDate, DateTime checkOutDate)
    {
        if (roomTypeId <= 0 || checkOutDate <= checkInDate || checkInDate < DateTime.Now.AddMinutes(-1))
        {
            return BadRequest(new { success = false, message = "Khoảng thời gian hoặc loại phòng không hợp lệ." });
        }

        var roomTypeExists = await _context.RoomTypes
            .AsNoTracking()
            .AnyAsync(roomType => roomType.RoomTypeId == roomTypeId &&
                                  roomType.Status &&
                                  HotelRoomTypeCatalog.Codes.Contains(roomType.Code));
        if (!roomTypeExists)
        {
            return BadRequest(new { success = false, message = "Loại phòng không còn nhận đặt." });
        }

        var conflictingCageIds = await _context.HotelBookings
            .AsNoTracking()
            .Where(booking => BlockingStatuses.Contains(booking.Status) &&
                              booking.CheckInDate < checkOutDate &&
                              (!booking.CheckOutDate.HasValue || booking.CheckOutDate.Value > checkInDate))
            .Select(booking => booking.CageId)
            .Distinct()
            .ToListAsync();

        var availableCages = await _context.Cages
            .AsNoTracking()
            .Where(cage => cage.RoomTypeId == roomTypeId &&
                           cage.Status == "Trống" &&
                           !conflictingCageIds.Contains(cage.CageId))
            .OrderBy(cage => cage.CageId)
            .Select(cage => new { cageId = cage.CageId })
            .ToListAsync();

        return Json(new { success = true, cages = availableCages });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    // [nam] Xác thực dữ liệu, giữ tồn kho thức ăn và tạo booking online trong một giao dịch.
    public async Task<IActionResult> Book([FromForm] HotelBookingRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BookingError(GetModelStateErrorMessage());
        }

        var customer = await GetCurrentCustomerAsync();
        if (customer == null)
        {
            return RedirectToAction("Login", "Account", new { area = "Customer" });
        }

        var petId = request.PetId!.Value;
        var roomTypeId = request.RoomTypeId!.Value;
        var requestedCageId = request.CageId.Trim().ToUpperInvariant();
        var checkIn = request.CheckInDate!.Value;
        var checkOut = request.CheckOutDate!.Value;
        var stayDays = HotelPricingPolicy.CalculateStayDays(checkIn, checkOut);

        await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        try
        {
            var pet = await _context.Pets
                .FirstOrDefaultAsync(p => p.PetId == petId && p.CustomerId == customer.CustomerId);

            if (pet == null)
            {
                return BookingError("Không tìm thấy thú cưng hoặc thú cưng không thuộc tài khoản của bạn.");
            }

            if (!string.Equals(pet.Status, "Active", StringComparison.OrdinalIgnoreCase))
            {
                return BookingError("Hồ sơ thú cưng đã chọn không còn hoạt động.");
            }

            if (pet.Weight <= 0)
            {
                return BookingError(
                    $"Hồ sơ của {pet.Name} chưa có cân nặng hợp lệ. " +
                    "Vui lòng cập nhật hồ sơ thú cưng trước khi đặt chuồng.");
            }

            var roomType = await _context.RoomTypes
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.RoomTypeId == roomTypeId &&
                                          r.Status &&
                                          HotelRoomTypeCatalog.Codes.Contains(r.Code));

            if (roomType == null)
            {
                return BookingError("Loại phòng đã chọn hiện không còn hoạt động.");
            }

            var foodProductSku = request.FoodProductSku.Trim();
            var foodProduct = await _context.Products
                .AsNoTracking()
                .Include(product => product.Category)
                .FirstOrDefaultAsync(product =>
                    product.Sku == foodProductSku &&
                    !product.IsDeleted &&
                    product.Stock > 0 &&
                    product.Unit == HotelFoodCatalog.DailyUnit &&
                    product.Category != null &&
                    !product.Category.IsDeleted &&
                    product.Category.Code == HotelFoodCatalog.CategoryCode);

            if (foodProduct == null)
            {
                return BookingError("Gói thức ăn không còn được cung cấp từ kho cửa hàng.");
            }

            if (!HotelFoodCatalog.IsSpeciesCompatible(foodProduct.AnimalType, pet.Species))
            {
                return BookingError("Gói thức ăn không phù hợp với loài của thú cưng.");
            }

            if (foodProduct.Price <= 0)
            {
                return BookingError("Gói thức ăn chưa có giá bán hợp lệ.");
            }

            var foodQuote = HotelFoodPricing.Calculate(foodProduct.Price, pet.Weight, stayDays);

            var reservedFoodUnits = await _context.HotelBookingFoodPlans
                .Where(plan => plan.ProductSku == foodProduct.Sku &&
                               plan.InventoryQuantityDeducted == 0 &&
                               BlockingStatuses.Contains(plan.HotelBooking.Status))
                .SumAsync(plan => (int?)plan.ChargeableDays) ?? 0;
            var availableFoodUnits = Math.Max(0, foodProduct.Stock - reservedFoodUnits);
            if (availableFoodUnits < foodQuote.InventoryUnits)
            {
                return BookingError(
                    $"{foodProduct.Name} chỉ còn {availableFoodUnits} suất chuẩn, " +
                    $"không đủ {foodQuote.InventoryUnits} suất cho {stayDays} ngày ({foodQuote.WeightBand}).");
            }

            var petHasConflict = await _context.HotelBookings.AnyAsync(b =>
                b.PetId == petId &&
                BlockingStatuses.Contains(b.Status) &&
                b.CheckInDate < checkOut &&
                (!b.CheckOutDate.HasValue || b.CheckOutDate.Value > checkIn));

            if (petHasConflict)
            {
                return BookingError($"{pet.Name} đã có lịch lưu trú trùng với khoảng ngày bạn chọn.");
            }

            var conflictingCageIds = await _context.HotelBookings
                .Where(b =>
                    BlockingStatuses.Contains(b.Status) &&
                    b.CheckInDate < checkOut &&
                    (!b.CheckOutDate.HasValue || b.CheckOutDate.Value > checkIn))
                .Select(b => b.CageId)
                .Distinct()
                .ToListAsync();

            var cage = await _context.Cages
                .AsNoTracking()
                .Where(c =>
                    c.CageId == requestedCageId &&
                    c.RoomTypeId == roomTypeId &&
                    c.Status == "Trống" &&
                    !conflictingCageIds.Contains(c.CageId))
                .FirstOrDefaultAsync();

            if (cage == null)
            {
                return BookingError("Chuồng đã chọn không còn trống trong khoảng thời gian này. Vui lòng chọn lại.");
            }

            var subtotal = roomType.DailyPrice * stayDays;
            var discountRate = ResolveDiscountRate(customer.MembershipTier);
            var discount = decimal.Round(subtotal * discountRate, 0, MidpointRounding.AwayFromZero);
            var foodPricePerDay = foodQuote.PricePerDay;
            var foodTotal = foodQuote.TotalAmount;
            var finalAmount = subtotal - discount + foodTotal;

            await _inventoryBatchService.DeductStockFIFO(foodProduct.Sku, foodQuote.InventoryUnits);

            var booking = new HotelBooking
            {
                CageId = cage.CageId,
                PetId = pet.PetId,
                CustomerId = customer.CustomerId,
                CheckInDate = checkIn,
                CheckOutDate = checkOut,
                ScheduledCheckInDate = checkIn,
                ScheduledCheckOutDate = checkOut,
                StayDays = stayDays,
                BaseDailyPrice = roomType.DailyPrice,
                Subtotal = subtotal,
                Discount = discount,
                FinalAmount = finalAmount,
                EarnedPoints = 0,
                Status = "Đã đặt"
            };

            _context.HotelBookings.Add(booking);
            _context.HotelBookingFoodPlans.Add(new HotelBookingFoodPlan
            {
                HotelBooking = booking,
                ProductSku = foodProduct.Sku,
                PlanType = "HotelProduct",
                FoodNameSnapshot = foodProduct.Name,
                ProductUnitSnapshot = foodProduct.Unit,
                BasePricePerDaySnapshot = foodQuote.BasePricePerDay,
                PetWeightSnapshot = foodQuote.PetWeightKg,
                PortionMultiplierSnapshot = foodQuote.PortionMultiplier,
                PricePerDaySnapshot = foodPricePerDay,
                PortionGrams = 0,
                MealsPerDay = 0,
                FeedingInstructions = null,
                AllergyNotes = string.IsNullOrWhiteSpace(request.AllergyNotes) ? null : request.AllergyNotes.Trim(),
                ChargeableDays = stayDays,
                InventoryQuantityDeducted = foodQuote.InventoryUnits,
                TotalAmount = foodTotal,
                CreatedAt = DateTime.Now
            });
            _context.PetBioTimelines.Add(new PetBioTimeline
            {
                PetId = pet.PetId,
                HotelBooking = booking,
                Date = DateTime.Now,
                Title = "Đặt chuồng lưu trú",
                Type = "HotelBookingCreated",
                Description = $"Khách hàng đặt chuồng {cage.CageId} từ {checkIn:dd/MM/yyyy HH:mm} đến {checkOut:dd/MM/yyyy HH:mm}; " +
                    $"gói ăn {foodProduct.Name} ({foodProduct.Sku}) {foodPricePerDay:N0}đ/ngày, " +
                    $"tạm tính theo cân nặng hồ sơ {foodQuote.PetWeightKg:0.##}kg, hệ số {foodQuote.PortionMultiplier:0.##} ({foodQuote.WeightBand}). " +
                    "Giá và khẩu phần cuối cùng được xác nhận khi tiếp nhận."
            });

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            await _hotelEmailService.SendBookingCreatedAsync(
                customer.Email,
                customer.FullName,
                booking.HotelBookingId,
                pet.Name,
                cage.CageId,
                roomType.Type,
                checkIn,
                checkOut,
                finalAmount);

            TempData["SuccessMessage"] =
                $"Đặt phòng thành công cho {pet.Name}. Chuồng dự kiến: {cage.CageId}.";
            return RedirectToAction(nameof(Index));
        }
        catch (ManagePetStore.Exceptions.ServiceException ex)
        {
            await transaction.RollbackAsync();
            return BookingError(ex.Message);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(
                ex,
                "Không thể đặt Hotel online cho CustomerId {CustomerId}, PetId {PetId}.",
                customer.CustomerId,
                petId);
            return BookingError("Không thể hoàn tất đặt phòng do lỗi hệ thống. Vui lòng thử lại.");
        }
    }


    // [nam] Lưu thông báo lỗi và điều hướng khách về trang đặt chuồng.
    private IActionResult BookingError(string message)
    {
        TempData["HotelError"] = message;
        return RedirectToAction("Index", "Home", new { area = "", hotel = "book" });
    }


    // [nam] Gom thông báo validation đầu tiên để hiển thị lại cho người dùng.
    private string GetModelStateErrorMessage()
    {
        return ModelState.Values
            .SelectMany(v => v.Errors)
            .Select(e => e.ErrorMessage)
            .FirstOrDefault(message => !string.IsNullOrWhiteSpace(message))
            ?? "Thông tin đặt phòng không hợp lệ.";
    }

}
