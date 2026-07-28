using System.Data;
using ManagePetStore.Areas.ServiceStaff.Models;
using ManagePetStore.Exceptions;
using ManagePetStore.Hubs;
using ManagePetStore.Models;
using ManagePetStore.Services.Warehouse;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace ManagePetStore.Services.Hotel;

public sealed class HotelReceptionService : IHotelReceptionService
{
    private static readonly string[] BlockingStatuses = ["Đã đặt", "Active", "Đang ở"];

    private readonly PetStoreManagementContext _context;
    private readonly IHotelAvailabilityService _availabilityService;
    private readonly IInventoryBatchService _inventoryBatchService;
    private readonly IHotelEmailService _hotelEmailService;
    private readonly IHubContext<HotelCareHub> _hotelCareHub;
    private readonly ILogger<HotelReceptionService> _logger;

    // [nam] Khởi tạo service tiếp nhận pet cùng các thành phần kiểm tra chuồng, kho và email.
    public HotelReceptionService(
        PetStoreManagementContext context,
        IHotelAvailabilityService availabilityService,
        IInventoryBatchService inventoryBatchService,
        IHotelEmailService hotelEmailService,
        IHubContext<HotelCareHub> hotelCareHub,
        ILogger<HotelReceptionService> logger)
    {
        _context = context;
        _availabilityService = availabilityService;
        _inventoryBatchService = inventoryBatchService;
        _hotelEmailService = hotelEmailService;
        _hotelCareHub = hotelCareHub;
        _logger = logger;
    }

    // [nam] Xác thực hồ sơ sức khỏe, phân chuồng, chốt thức ăn và tiếp nhận pet.
    public async Task<HotelCommandResult> CheckInAsync(
        HotelCheckInRequest request,
        int? staffUserId,
        string staffName)
    {
        // [nam][Flow] Chuẩn hóa dữ liệu định danh; toàn bộ luồng nhận pet dùng giờ server, không tin thời gian phía client.
        string customerPhone = DigitsOnly(request.CustomerPhone);
        string cageId = request.CageId.Trim().ToUpperInvariant();
        string healthNote = request.HealthNote?.Trim() ?? string.Empty;
        DateTime actualCheckInAt = DateTime.Now;
        DateTime checkInDate = actualCheckInAt;
        DateTime? checkOutDate = request.CheckOutDate;

        // [nam][Validate] Lặp lại luật thời gian ở service tại đúng mốc server dùng để ghi nhận và tính lịch chuồng.
        if (!checkOutDate.HasValue || checkOutDate.Value <= actualCheckInAt)
        {
            return HotelCommandResult.Fail("Ngày trả dự kiến phải sau thời gian tiếp nhận.");
        }

        if ((checkOutDate.Value - actualCheckInAt).TotalDays > 365)
        {
            return HotelCommandResult.Fail("Thời gian lưu trú dự kiến không được vượt quá 365 ngày.");
        }

        if (!HotelOperatingHoursPolicy.IsExpectedCheckoutWithinHandoverHours(checkOutDate.Value))
        {
            return HotelCommandResult.Fail(HotelOperatingHoursPolicy.ExpectedCheckoutError);
        }

        // [nam][BR] Khóa transaction ở mức Serializable để kết quả kiểm tra chuồng và tồn kho không bị thay đổi giữa chừng.
        await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        try
        {
            // [nam][Validate] Chuồng phải tồn tại, đang trống và thuộc đúng loại chuồng còn hoạt động.
            var cage = await _context.Cages
                .Include(item => item.RoomType)
                .FirstOrDefaultAsync(item => item.CageId == cageId);
            if (cage == null)
            {
                return HotelCommandResult.Fail("Không tìm thấy chuồng đã chọn.");
            }

            if (cage.Status != "Trống")
            {
                return HotelCommandResult.Fail($"Chuồng {cageId} hiện không còn trống.");
            }

            if (cage.RoomType == null || !cage.RoomType.Status || cage.RoomTypeId != request.RoomTypeId)
            {
                return HotelCommandResult.Fail("Chuồng đã chọn không thuộc loại chuồng đang hoạt động.");
            }

            if (cage.RoomType.DailyPrice <= 0 ||
                cage.RoomType.HourlyPrice <= 0 ||
                cage.RoomType.HourlyPrice > cage.RoomType.DailyPrice)
            {
                return HotelCommandResult.Fail("Bảng giá ngày/giờ của loại chuồng chưa hợp lệ.");
            }

            // [nam][Validate] Mỗi sổ y tế chỉ được gắn một lượt lưu trú và phải có cân nặng hợp lệ.
            var medicalRecord = await _context.MedicalRecords
                .Include(record => record.Pet)
                    .ThenInclude(pet => pet.Customer)
                .FirstOrDefaultAsync(record => record.RecordId == request.MedicalRecordId!.Value);
            if (medicalRecord == null)
            {
                return HotelCommandResult.Fail("Không tìm thấy sổ y tế đã chọn.");
            }

            if (medicalRecord.HotelBookingId.HasValue)
            {
                return HotelCommandResult.Fail("Sổ y tế này đã được sử dụng cho một lượt lưu trú khác.");
            }

            if (medicalRecord.Weight <= 0)
            {
                return HotelCommandResult.Fail(
                    "Sổ y tế chưa có cân nặng hợp lệ. Vui lòng cập nhật sổ trước khi tiếp nhận vào chuồng.");
            }

            // [nam][Validate] SĐT được đối chiếu với chủ của pet trong sổ y tế để tránh tiếp nhận nhầm pet trùng tên.
            var pet = medicalRecord.Pet;
            var customer = pet.Customer;
            if (!string.Equals(DigitsOnly(customer.Phone), customerPhone, StringComparison.Ordinal))
            {
                return HotelCommandResult.Fail("Số điện thoại không khớp với chủ của sổ y tế đã chọn.");
            }

            if (!string.Equals(pet.Status, "Active", StringComparison.OrdinalIgnoreCase))
            {
                return HotelCommandResult.Fail("Hồ sơ thú cưng đã chọn không còn hoạt động.");
            }

            // [nam][Flow] Có HotelBookingId thì nối vào booking online; không có thì tạo lượt gửi trực tiếp tại quầy.
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
                    return HotelCommandResult.Fail("Không tìm thấy lịch đặt online đang chờ tiếp nhận.");
                }

                if (onlineReservation.PetId != pet.PetId ||
                    onlineReservation.CustomerId != customer.CustomerId)
                {
                    return HotelCommandResult.Fail(
                        "Lịch đặt online không khớp với chủ nuôi hoặc thú cưng đã chọn.");
                }

                // [nam] Tạm thời cho phép Staff tiếp nhận booking trước lịch dự kiến.
            }

            // [nam][BR] Một pet chỉ có một booking chặn tại một thời điểm; loại chính booking online đang tiếp nhận khỏi phép kiểm tra.
            int excludedBookingId = onlineReservation?.HotelBookingId ?? 0;
            bool petHasBlockingBooking = await _context.HotelBookings.AnyAsync(booking =>
                booking.PetId == pet.PetId &&
                booking.HotelBookingId != excludedBookingId &&
                BlockingStatuses.Contains(booking.Status));
            if (petHasBlockingBooking)
            {
                return HotelCommandResult.Fail(
                    $"{pet.Name} đã có lịch đặt hoặc đang lưu trú, không thể tiếp nhận thêm.");
            }

            if (onlineReservation != null &&
                !string.Equals(onlineReservation.CageId, cageId, StringComparison.OrdinalIgnoreCase))
            {
                return HotelCommandResult.Fail(
                    $"{pet.Name} đã đặt online chuồng {onlineReservation.CageId} trong ngày nhận này. " +
                    "Vui lòng chọn đúng chuồng đã giữ.");
            }

            // [nam][Validate] Gói ăn phải thuộc danh mục Hotel, đúng đơn vị ngày, đúng loài và có giá hợp lệ.
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
                return HotelCommandResult.Fail("Gói thức ăn đã chọn không còn sẵn trong kho cửa hàng.");
            }

            if (!HotelFoodCatalog.IsSpeciesCompatible(foodProduct.AnimalType, pet.Species))
            {
                return HotelCommandResult.Fail("Gói thức ăn đã chọn không phù hợp với loài của thú cưng.");
            }

            if (foodProduct.Price <= 0 && (onlineReservation?.FoodPlan?.BasePricePerDaySnapshot ?? 0) <= 0)
            {
                return HotelCommandResult.Fail("Gói thức ăn chưa có giá bán hợp lệ.");
            }

            // [nam][BR] Kiểm tra lại trùng lịch pet và chuồng ngay trước khi ghi, kể cả giao diện đã kiểm tra trước đó.
            if (await _availabilityService.HasPetConflictAsync(
                    pet.PetId,
                    checkInDate,
                    checkOutDate,
                    excludedBookingId))
            {
                return HotelCommandResult.Fail(
                    $"{pet.Name} có lịch lưu trú khác trùng với khoảng thời gian tiếp nhận.");
            }

            if (await _availabilityService.HasCageConflictAsync(
                    cageId,
                    checkInDate,
                    checkOutDate,
                    excludedBookingId))
            {
                return HotelCommandResult.Fail(
                    $"Chuồng {cageId} đã được giữ cho một lịch lưu trú khác trong khoảng thời gian này.");
            }

            // [nam][BR] Booking online giữ snapshot giá phòng; gửi trực tiếp dùng bảng giá đang hiệu lực tại lúc tiếp nhận.
            DateTime servicePricingStart = onlineReservation?.ScheduledCheckInDate
                ?? onlineReservation?.CheckInDate
                ?? checkInDate;
            int estimatedStayDays = HotelPricingPolicy.CalculateStayDays(
                servicePricingStart,
                checkOutDate ?? checkInDate.AddDays(1));
            decimal dailyPrice = onlineReservation?.BaseDailyPrice > 0
                ? onlineReservation.BaseDailyPrice
                : cage.RoomType.DailyPrice;
            var roomQuote = HotelPricingPolicy.CalculateRoomCharge(
                checkInDate,
                checkOutDate ?? checkInDate.AddDays(1),
                dailyPrice,
                cage.RoomType.HourlyPrice);
            decimal subtotal = onlineReservation?.Subtotal > 0
                ? onlineReservation.Subtotal
                : roomQuote.TotalAmount;
            bool keepReservedFoodSnapshot = onlineReservation?.FoodPlan?.ProductSku == foodProduct.Sku;
            decimal baseFoodPricePerDay = keepReservedFoodSnapshot &&
                onlineReservation!.FoodPlan!.BasePricePerDaySnapshot > 0
                    ? onlineReservation.FoodPlan.BasePricePerDaySnapshot
                    : foodProduct.Price;
            var foodQuote = HotelFoodPricing.Calculate(
                baseFoodPricePerDay,
                medicalRecord.Weight,
                estimatedStayDays);
            decimal reservedFoodPricePerDay = onlineReservation?.FoodPlan?.PricePerDaySnapshot ?? 0;
            decimal? reservedFoodWeight = onlineReservation?.FoodPlan?.PetWeightSnapshot;
            decimal reservedFoodMultiplier = onlineReservation?.FoodPlan?.PortionMultiplierSnapshot ?? 0;
            // [nam][BR] Cân nặng sổ y tế là dữ liệu chốt; nếu làm đổi giá gói ăn, Staff phải xác nhận rõ trước khi tiếp nhận.
            bool foodPriceChanged = reservedFoodPricePerDay > 0 &&
                reservedFoodPricePerDay != foodQuote.PricePerDay;
            if (foodPriceChanged && !request.FoodPriceChangeConfirmed)
            {
                string reservedWeightText = reservedFoodWeight.HasValue
                    ? $"{reservedFoodWeight:0.##}kg, hệ số {reservedFoodMultiplier:0.##}"
                    : "cân nặng trong hồ sơ khi đặt";
                return HotelCommandResult.Fail(
                    $"Giá thức ăn thay đổi từ {reservedFoodPricePerDay:N0}đ/ngày ({reservedWeightText}) " +
                    $"sang {foodQuote.PricePerDay:N0}đ/ngày theo sổ y tế {medicalRecord.Weight:0.##}kg, " +
                    $"hệ số {foodQuote.PortionMultiplier:0.##}. Staff phải xác nhận mức giá mới trước khi tiếp nhận.");
            }

            // [nam][Validate] Cộng lại phần kho booking hiện tại đã giữ rồi trừ các reservation khác để tránh báo thiếu giả.
            int currentBookingId = onlineReservation?.HotelBookingId ?? 0;
            int reservedFoodUnits = await _context.HotelBookingFoodPlans
                .Where(plan =>
                    plan.ProductSku == foodProduct.Sku &&
                    plan.HotelBookingId != currentBookingId &&
                    plan.InventoryQuantityDeducted == 0 &&
                    BlockingStatuses.Contains(plan.HotelBooking.Status))
                .SumAsync(plan => (int?)plan.ChargeableDays) ?? 0;
            int currentReservedFoodUnits = onlineReservation?.FoodPlan?.ProductSku == foodProduct.Sku
                ? onlineReservation.FoodPlan.InventoryQuantityDeducted
                : 0;
            int availableFoodUnits = Math.Max(
                0,
                foodProduct.Stock + currentReservedFoodUnits - reservedFoodUnits);
            if (availableFoodUnits < foodQuote.InventoryUnits)
            {
                return HotelCommandResult.Fail(
                    $"{foodProduct.Name} chỉ còn {availableFoodUnits} suất chuẩn, " +
                    $"không đủ {foodQuote.InventoryUnits} suất cho {estimatedStayDays} ngày ({foodQuote.WeightBand}).");
            }

            decimal foodPricePerDay = foodQuote.PricePerDay;
            decimal foodTotal = foodQuote.TotalAmount;
            // [nam][Flow] Booking online được chuyển trạng thái; lượt trực tiếp tạo booking mới và vẫn đi qua cùng luồng checkout.
            HotelBooking hotelBooking;
            if (onlineReservation != null)
            {
                // [nam][BR] Giữ nguyên lịch Customer đã đặt, còn mốc vận hành hiện tại dùng giờ nhận thật và ngày trả Staff xác nhận.
                onlineReservation.ScheduledCheckInDate ??= onlineReservation.CheckInDate;
                onlineReservation.ScheduledCheckOutDate ??= onlineReservation.CheckOutDate;
                onlineReservation.CheckInDate = checkInDate;
                onlineReservation.CheckOutDate = checkOutDate;
                onlineReservation.ActualCheckInAt = actualCheckInAt;
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
                    ActualCheckInAt = actualCheckInAt,
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

            // [nam][Flow] Đổi gói ăn thì hoàn gói cũ trước, sau đó đối soát đúng phần chênh lệch của gói mới.
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

            // [nam][Flow] Mọi thay đổi giá do cân nặng được lưu đồng thời vào notification và timeline để có thể đối soát.
            CustomerNotification? foodPriceNotification = null;
            if (foodPriceChanged)
            {
                decimal oldFoodTotal = reservedFoodPricePerDay * estimatedStayDays;
                decimal newFoodTotal = foodQuote.TotalAmount;
                string reservedWeightText = reservedFoodWeight.HasValue
                    ? $"{reservedFoodWeight:0.##}kg, hệ số {reservedFoodMultiplier:0.##}"
                    : "cân nặng trong hồ sơ khi đặt";
                string adjustmentMessage =
                    $"Khi tiếp nhận {pet.Name}, giá gói {foodPlan.FoodNameSnapshot} được xác nhận lại từ " +
                    $"{reservedFoodPricePerDay:N0}đ/ngày ({reservedWeightText}) thành " +
                    $"{foodQuote.PricePerDay:N0}đ/ngày theo cân nặng sổ y tế {medicalRecord.Weight:0.##}kg, " +
                    $"hệ số {foodQuote.PortionMultiplier:0.##}. Tổng thức ăn dự kiến cho {estimatedStayDays} ngày " +
                    $"thay đổi từ {oldFoodTotal:N0}đ thành {newFoodTotal:N0}đ.";

                foodPriceNotification = new CustomerNotification
                {
                    CustomerId = customer.CustomerId,
                    HotelBooking = hotelBooking,
                    Type = "HotelFoodPriceAdjusted",
                    Title = $"Điều chỉnh giá thức ăn của {pet.Name}",
                    Message = adjustmentMessage,
                    LinkUrl = $"/Customer/HotelBooking/Details/{hotelBooking.HotelBookingId}",
                    IsRead = false,
                    CreatedAt = DateTime.Now
                };
                _context.CustomerNotifications.Add(foodPriceNotification);
                _context.PetBioTimelines.Add(new PetBioTimeline
                {
                    PetId = pet.PetId,
                    HotelBooking = hotelBooking,
                    Date = DateTime.Now,
                    Title = "Điều chỉnh giá thức ăn khi tiếp nhận",
                    Type = "HotelFoodPriceAdjusted",
                    Description = adjustmentMessage + $" Nhân viên xác nhận: {staffName}."
                });
            }

            _context.PetBioTimelines.Add(new PetBioTimeline
            {
                PetId = pet.PetId,
                HotelBooking = hotelBooking,
                Date = DateTime.Now,
                Title = "Kiểm tra sức khỏe đầu vào",
                Type = "HealthCheckIn",
                Description = BuildHealthCheckDescription(
                    request,
                    medicalRecord,
                    healthNote,
                    staffName)
            });
            _context.PetBioTimelines.Add(new PetBioTimeline
            {
                PetId = pet.PetId,
                HotelBooking = hotelBooking,
                Date = DateTime.Now,
                Title = "Tiếp nhận lưu trú",
                Type = "PetCheckIn",
                Description = BuildPetCheckInDescription(
                    actualCheckInAt,
                    cageId,
                    customer.FullName,
                    foodPlan.FoodNameSnapshot,
                    checkOutDate,
                    staffName)
            });

            // [nam][Flow] Lưu kết quả sức khỏe và mở phân đoạn ở chuồng trước khi đổi chuồng sang trạng thái đang dùng.
            _context.HotelCheckInAssessments.Add(new HotelCheckInAssessment
            {
                HotelBooking = hotelBooking,
                MedicalRecord = medicalRecord,
                Decision = request.HealthStatus,
                Note = string.IsNullOrWhiteSpace(healthNote) ? null : healthNote,
                AssessedByUserId = staffUserId,
                AssessedByName = staffName,
                AssessedAt = DateTime.Now
            });

            medicalRecord.HotelBooking = hotelBooking;
            _context.HotelCageStaySegments.Add(new HotelCageStaySegment
            {
                HotelBooking = hotelBooking,
                CageId = cage.CageId,
                RoomTypeId = cage.RoomTypeId,
                DailyPriceSnapshot = dailyPrice,
                StartedAt = actualCheckInAt,
                StartReason = "CheckIn",
                CreatedAt = DateTime.Now
            });
            cage.Status = "Đang dùng";

            // [nam][Flow] Chỉ phát realtime/email sau khi toàn bộ booking, kho, chuồng và lịch sử đã commit thành công.
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            if (foodPriceNotification != null)
            {
                try
                {
                    await _hotelCareHub.Clients
                        .Group(HotelCareHub.GroupName(customer.CustomerId))
                        .SendAsync("HotelNotificationCreated", new
                        {
                            notificationId = foodPriceNotification.NotificationId,
                            bookingId = hotelBooking.HotelBookingId,
                            petName = pet.Name,
                            title = foodPriceNotification.Title,
                            message = foodPriceNotification.Message,
                            occurredAt = foodPriceNotification.CreatedAt,
                            linkUrl = foodPriceNotification.LinkUrl
                        });
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(
                        ex,
                        "Food price notification was saved but realtime delivery failed for customer {CustomerId}.",
                        customer.CustomerId);
                }
            }

            await _hotelEmailService.SendCheckInAsync(
                customer.Email,
                customer.FullName,
                hotelBooking.HotelBookingId,
                pet.Name,
                cageId,
                checkInDate,
                checkOutDate);

            string successMessage = $"Đã hoàn tất tiếp nhận lưu trú cho {pet.Name} tại chuồng {cageId}!";
            if (foodPriceChanged)
            {
                successMessage +=
                    $" Giá thức ăn đã được xác nhận từ {reservedFoodPricePerDay:N0}đ/ngày " +
                    $"sang {foodQuote.PricePerDay:N0}đ/ngày và đã gửi thông báo trên web cho khách hàng.";
            }

            return HotelCommandResult.Ok(successMessage);
        }
        catch (ServiceException ex)
        {
            await transaction.RollbackAsync();
            return HotelCommandResult.Fail(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            await transaction.RollbackAsync();
            return HotelCommandResult.Fail(ex.Message);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(
                ex,
                "Không thể kiểm tra sức khỏe và tiếp nhận thú cưng vào chuồng {CageId}",
                cageId);
            return HotelCommandResult.Fail(
                "Không thể tiếp nhận thú cưng do lỗi hệ thống. Vui lòng thử lại.");
        }
    }

    // [nam] Từ chối tiếp nhận pet sau kiểm tra sức khỏe và giải phóng tài nguyên đã giữ.
    public async Task<HotelCommandResult> RejectAsync(
        HotelCheckInRequest request,
        int? staffUserId,
        string staffName)
    {
        // [nam][BR] Từ chối là một lệnh nguyên tử: lưu đánh giá, đổi trạng thái và hoàn kho phải cùng thành công hoặc cùng rollback.
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
                return HotelCommandResult.Fail("Booking không còn ở trạng thái chờ tiếp nhận.");
            }

            // [nam] Tạm thời cho phép Staff xử lý booking trước lịch dự kiến.

            // [nam][BR] Mỗi booking chỉ có một kết luận kiểm tra đầu vào để tránh Staff xử lý lặp.
            if (booking.CheckInAssessment != null)
            {
                return HotelCommandResult.Fail("Booking này đã có kết luận kiểm tra sức khỏe.");
            }

            // [nam][Validate] Sổ từ chối vẫn phải đúng pet, chưa dùng và có cân nặng hợp lệ.
            var medicalRecord = await _context.MedicalRecords
                .FirstOrDefaultAsync(record =>
                    record.RecordId == request.MedicalRecordId!.Value &&
                    record.PetId == booking.PetId &&
                    record.HotelBookingId == null &&
                    record.Weight > 0);
            if (medicalRecord == null)
            {
                return HotelCommandResult.Fail(
                    "Sổ y tế không còn khả dụng hoặc không thuộc pet trong booking.");
            }

            if (!string.Equals(
                    DigitsOnly(request.CustomerPhone),
                    DigitsOnly(booking.Pet.Customer.Phone),
                    StringComparison.Ordinal))
            {
                return HotelCommandResult.Fail("Số điện thoại không khớp với chủ của pet trong booking.");
            }

            // [nam][Flow] Từ chối trước check-in phải giải phóng toàn bộ suất ăn đã giữ và chống hoàn kho lặp.
            if (booking.FoodPlan?.ProductSku != null && booking.FoodPlan.InventoryQuantityDeducted > 0)
            {
                await _inventoryBatchService.RestockToBatches(
                    booking.FoodPlan.ProductSku,
                    booking.FoodPlan.InventoryQuantityDeducted);
                booking.FoodPlan.InventoryQuantityDeducted = 0;
            }

            string rejectionNote = request.HealthNote!.Trim();
            _context.HotelCheckInAssessments.Add(new HotelCheckInAssessment
            {
                HotelBooking = booking,
                MedicalRecord = medicalRecord,
                Decision = HotelCheckInRequest.RejectedStatus,
                Note = rejectionNote,
                AssessedByUserId = staffUserId,
                AssessedByName = staffName,
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
                    $"Người đánh giá: {staffName}. Chuồng và suất ăn đã giữ được giải phóng."
            });
            _context.CustomerNotifications.Add(new CustomerNotification
            {
                CustomerId = booking.CustomerId,
                HotelBooking = booking,
                Type = "HotelReceptionRejected",
                Title = $"Không thể tiếp nhận {booking.Pet.Name}",
                Message = $"Booking #{booking.HotelBookingId} bị từ chối tiếp nhận sau kiểm tra sức khỏe. " +
                    $"Lý do: {rejectionNote}",
                LinkUrl = $"/Customer/HotelBooking/Details/{booking.HotelBookingId}",
                IsRead = false,
                CreatedAt = DateTime.Now
            });

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            return HotelCommandResult.Ok(
                $"Đã lưu quyết định từ chối tiếp nhận {booking.Pet.Name} và hoàn lại tài nguyên đã giữ.");
        }
        catch (ServiceException ex)
        {
            await transaction.RollbackAsync();
            return HotelCommandResult.Fail(ex.Message);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(
                ex,
                "Không thể lưu quyết định từ chối booking Hotel {BookingId}.",
                request.HotelBookingId);
            return HotelCommandResult.Fail(
                "Không thể lưu quyết định từ chối do lỗi hệ thống. Vui lòng thử lại.");
        }
    }

    // [nam] Tạo nội dung timeline mô tả kết quả kiểm tra sức khỏe đầu vào.
    private static string BuildHealthCheckDescription(
        HotelCheckInRequest request,
        MedicalRecord medicalRecord,
        string healthNote,
        string staffName)
    {
        string conclusion = request.HealthStatus == HotelCheckInRequest.FitStatus
            ? "Đủ điều kiện lưu trú"
            : "Đủ điều kiện nhưng cần theo dõi";

        return $"Hình thức tiếp nhận: Dùng sổ y tế có sẵn\n"
             + $"Sổ y tế: #{medicalRecord.RecordId} - khám ngày {medicalRecord.DateCreated:dd/MM/yyyy HH:mm}\n"
             + $"Tình trạng trong sổ: {medicalRecord.HealthStatus}\n"
             + $"Cân nặng trong sổ: {medicalRecord.Weight:0.##} kg\n"
             + $"Triệu chứng/bệnh lý: {(string.IsNullOrWhiteSpace(medicalRecord.Symptoms) ? "Không ghi nhận" : medicalRecord.Symptoms)}\n"
             + $"Kết luận: {conclusion}\n"
             + $"Ghi chú tiếp nhận: {(string.IsNullOrWhiteSpace(healthNote) ? "Không có" : healthNote)}\n"
             + $"Người kiểm tra: {staffName}";
    }

    // [nam] Tạo nội dung timeline mô tả lần tiếp nhận pet vào chuồng.
    private static string BuildPetCheckInDescription(
        DateTime actualCheckInAt,
        string cageId,
        string customerName,
        string foodPlanName,
        DateTime? checkOutDate,
        string staffName)
    {
        string expectedCheckout = checkOutDate.HasValue
            ? checkOutDate.Value.TimeOfDay == TimeSpan.Zero
                ? checkOutDate.Value.ToString("dd/MM/yyyy")
                : checkOutDate.Value.ToString("dd/MM/yyyy HH:mm")
            : "Chưa xác định";

        return $"Hình thức tiếp nhận: Dùng sổ y tế có sẵn\n"
             + $"Chuồng tiếp nhận: {cageId}\n"
             + $"Chủ thú cưng: {customerName}\n"
             + $"Kế hoạch ăn: {foodPlanName}\n"
             + $"Ngày nhận: {actualCheckInAt:dd/MM/yyyy HH:mm}\n"
             + $"Ngày trả dự kiến: {expectedCheckout}\n"
             + $"Nhân viên tiếp nhận: {staffName}";
    }

    // [nam] Chuẩn hóa số điện thoại về chuỗi chỉ gồm chữ số để đối chiếu chủ pet.
    private static string DigitsOnly(string? value) =>
        new((value ?? string.Empty).Where(char.IsDigit).ToArray());
}
