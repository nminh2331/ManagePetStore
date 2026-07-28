using System.Data;
using ManagePetStore.Areas.Customer.Models;
using ManagePetStore.Exceptions;
using ManagePetStore.Models;
using ManagePetStore.Services.Warehouse;
using Microsoft.EntityFrameworkCore;

namespace ManagePetStore.Services.Hotel;

public sealed class HotelBookingService : IHotelBookingService
{
    private static readonly string[] BlockingStatuses = ["Đã đặt", "Active", "Đang ở"];

    private readonly PetStoreManagementContext _context;
    private readonly IHotelAvailabilityService _availabilityService;
    private readonly IInventoryBatchService _inventoryBatchService;
    private readonly IHotelEmailService _hotelEmailService;
    private readonly IStockMovementService _stockMovementService;
    private readonly ILogger<HotelBookingService> _logger;

    // [nam] Khởi tạo service đặt chuồng cùng các thành phần kiểm tra kho và gửi email.
    public HotelBookingService(
        PetStoreManagementContext context,
        IHotelAvailabilityService availabilityService,
        IInventoryBatchService inventoryBatchService,
        IHotelEmailService hotelEmailService,
        IStockMovementService stockMovementService,
        ILogger<HotelBookingService> logger)
    {
        _context = context;
        _availabilityService = availabilityService;
        _inventoryBatchService = inventoryBatchService;
        _hotelEmailService = hotelEmailService;
        _stockMovementService = stockMovementService;
        _logger = logger;
    }

    // [nam] Kiểm tra pet, chuồng, thời gian, thức ăn và tạo booking Hotel trong một transaction.
    public async Task<HotelCommandResult> CreateAsync(HotelBookingRequest request, int customerId)
    {
        // [nam][Flow] Chuẩn hóa khóa và thời gian một lần trước khi bắt đầu các truy vấn nghiệp vụ.
        int petId = request.PetId!.Value;
        int roomTypeId = request.RoomTypeId!.Value;
        string requestedCageId = request.CageId.Trim().ToUpperInvariant();
        DateTime checkIn = request.CheckInDate!.Value;
        DateTime checkOut = request.CheckOutDate!.Value;
        int stayDays = HotelPricingPolicy.CalculateStayDays(checkIn, checkOut);

        // [nam][Validate] Service tự bảo vệ luật giờ tiếp nhận/bàn giao kể cả khi request không đi qua giao diện đặt chuồng.
        if (!HotelOperatingHoursPolicy.IsExpectedCheckInWithinHandoverHours(checkIn))
        {
            return HotelCommandResult.Fail(HotelOperatingHoursPolicy.ExpectedCheckInError);
        }

        if (!HotelOperatingHoursPolicy.IsExpectedCheckoutWithinHandoverHours(checkOut))
        {
            return HotelCommandResult.Fail(HotelOperatingHoursPolicy.ExpectedCheckoutError);
        }

        // [nam][BR] Serializable khóa luồng kiểm tra và ghi để hai khách không đặt cùng chuồng/suất ăn đồng thời.
        await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        try
        {
            // [nam][Validate] Pet phải thuộc đúng Customer, còn hoạt động và có cân nặng để tính khẩu phần.
            var customer = await _context.Customers.FirstOrDefaultAsync(item => item.CustomerId == customerId);
            if (customer == null)
            {
                return HotelCommandResult.Fail("Không tìm thấy hồ sơ khách hàng.");
            }

            var pet = await _context.Pets
                .FirstOrDefaultAsync(item => item.PetId == petId && item.CustomerId == customerId);
            if (pet == null)
            {
                return HotelCommandResult.Fail("Không tìm thấy thú cưng hoặc thú cưng không thuộc tài khoản của bạn.");
            }

            if (!string.Equals(pet.Status, "Active", StringComparison.OrdinalIgnoreCase))
            {
                return HotelCommandResult.Fail("Hồ sơ thú cưng đã chọn không còn hoạt động.");
            }

            if (pet.Weight <= 0)
            {
                return HotelCommandResult.Fail(
                    $"Hồ sơ của {pet.Name} chưa có cân nặng hợp lệ. " +
                    "Vui lòng cập nhật hồ sơ thú cưng trước khi đặt chuồng.");
            }

            // [nam][Validate] Chỉ nhận loại chuồng thuộc danh mục Hotel và đang được phép kinh doanh.
            var roomType = await _context.RoomTypes
                .AsNoTracking()
                .FirstOrDefaultAsync(item =>
                    item.RoomTypeId == roomTypeId &&
                    item.Status &&
                    HotelRoomTypeCatalog.Codes.Contains(item.Code));
            if (roomType == null)
            {
                return HotelCommandResult.Fail("Loại phòng đã chọn hiện không còn hoạt động.");
            }

            if (roomType.DailyPrice <= 0 || roomType.HourlyPrice <= 0 || roomType.HourlyPrice > roomType.DailyPrice)
            {
                return HotelCommandResult.Fail("Bảng giá ngày/giờ của loại phòng chưa hợp lệ.");
            }

            // [nam][Validate] Gói ăn phải là sản phẩm theo ngày, còn bán và tương thích loài của pet.
            string foodProductSku = request.FoodProductSku.Trim();
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
                return HotelCommandResult.Fail("Gói thức ăn không còn được cung cấp từ kho cửa hàng.");
            }

            if (!HotelFoodCatalog.IsSpeciesCompatible(foodProduct.AnimalType, pet.Species))
            {
                return HotelCommandResult.Fail("Gói thức ăn không phù hợp với loài của thú cưng.");
            }

            if (foodProduct.Price <= 0)
            {
                return HotelCommandResult.Fail("Gói thức ăn chưa có giá bán hợp lệ.");
            }

            // [nam][BR] Giá và số suất kho phụ thuộc cân nặng pet cùng tổng số ngày tính phí.
            var foodQuote = HotelFoodPricing.Calculate(foodProduct.Price, pet.Weight, stayDays);
            int reservedFoodUnits = await _context.HotelBookingFoodPlans
                .Where(plan =>
                    plan.ProductSku == foodProduct.Sku &&
                    plan.InventoryQuantityDeducted == 0 &&
                    BlockingStatuses.Contains(plan.HotelBooking.Status))
                .SumAsync(plan => (int?)plan.ChargeableDays) ?? 0;
            int availableFoodUnits = Math.Max(0, foodProduct.Stock - reservedFoodUnits);
            if (availableFoodUnits < foodQuote.InventoryUnits)
            {
                return HotelCommandResult.Fail(
                    $"{foodProduct.Name} chỉ còn {availableFoodUnits} suất chuẩn, " +
                    $"không đủ {foodQuote.InventoryUnits} suất cho {stayDays} ngày ({foodQuote.WeightBand}).");
            }

            // [nam][BR] Một pet không được có hai lượt lưu trú giao nhau, dù dùng hai chuồng khác nhau.
            if (await _availabilityService.HasPetConflictAsync(petId, checkIn, checkOut))
            {
                return HotelCommandResult.Fail($"{pet.Name} đã có lịch lưu trú trùng với khoảng ngày bạn chọn.");
            }

            // [nam][BR] Chuồng phải đúng loại, đang trống vận hành và không bị booking khác chiếm lịch.
            var cage = await _context.Cages
                .AsNoTracking()
                .FirstOrDefaultAsync(item =>
                    item.CageId == requestedCageId &&
                    item.RoomTypeId == roomTypeId &&
                    item.Status == "Trống");
            if (cage == null ||
                await _availabilityService.HasCageConflictAsync(requestedCageId, checkIn, checkOut))
            {
                return HotelCommandResult.Fail(
                    "Chuồng đã chọn không còn trống trong khoảng thời gian này. Vui lòng chọn lại.");
            }

            // [nam][BR] Chụp giá tại thời điểm đặt để thay đổi bảng giá sau này không làm đổi booking cũ.
            var roomQuote = HotelPricingPolicy.CalculateRoomCharge(
                checkIn,
                checkOut,
                roomType.DailyPrice,
                roomType.HourlyPrice);
            decimal subtotal = roomQuote.TotalAmount;
            decimal discountRate = HotelPricingPolicy.ResolveMembershipDiscountRate(customer.MembershipTier);
            decimal discount = decimal.Round(subtotal * discountRate, 0, MidpointRounding.AwayFromZero);
            decimal foodPricePerDay = foodQuote.PricePerDay;
            decimal foodTotal = foodQuote.TotalAmount;
            decimal finalAmount = subtotal - discount + foodTotal;

            // [nam][Flow] Trừ FIFO và tạo booking trong cùng transaction; lỗi ở bước sau sẽ rollback toàn bộ.
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
                AllergyNotes = string.IsNullOrWhiteSpace(request.AllergyNotes)
                    ? null
                    : request.AllergyNotes.Trim(),
                ChargeableDays = stayDays,
                InventoryQuantityDeducted = foodQuote.InventoryUnits,
                TotalAmount = foodTotal,
                CreatedAt = DateTime.Now
            });
            // [nam][Flow] Timeline là dấu vết nghiệp vụ để Customer và Staff xem lại lịch sử lưu trú.
            _context.PetBioTimelines.Add(new PetBioTimeline
            {
                PetId = pet.PetId,
                HotelBooking = booking,
                Date = DateTime.Now,
                Title = "Đặt chuồng lưu trú",
                Type = "HotelBookingCreated",
                Description = $"Khách hàng đặt chuồng {cage.CageId} từ {checkIn:dd/MM/yyyy HH:mm} đến {checkOut:dd/MM/yyyy HH:mm}; " +
                    $"tiền phòng {roomQuote.DurationText}: {roomQuote.TotalAmount:N0}đ; " +
                    $"gói ăn {foodProduct.Name} ({foodProduct.Sku}) {foodPricePerDay:N0}đ/ngày, " +
                    $"tạm tính theo cân nặng hồ sơ {foodQuote.PetWeightKg:0.##}kg, hệ số {foodQuote.PortionMultiplier:0.##} ({foodQuote.WeightBand}). " +
                    "Giá và khẩu phần cuối cùng được xác nhận khi tiếp nhận."
            });

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            // [nam][Flow] Chỉ gửi email sau commit để không thông báo một booking chưa được lưu thành công.
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

            return HotelCommandResult.Ok(
                $"Đặt phòng thành công cho {pet.Name}. Chuồng dự kiến: {cage.CageId}.");
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
                "Không thể đặt Hotel online cho CustomerId {CustomerId}, PetId {PetId}.",
                customerId,
                petId);
            return HotelCommandResult.Fail(
                "Không thể hoàn tất đặt phòng do lỗi hệ thống. Vui lòng thử lại.");
        }
    }

    // [nam] Hủy booking của khách, hoàn tồn thức ăn và ghi lại hoạt động hủy.
    public async Task<HotelCommandResult> CancelAsync(int bookingId, int customerId)
    {
        // [nam][Validate] Ghép CustomerId vào truy vấn để ngăn khách hủy booking không thuộc quyền sở hữu.
        var booking = await _context.HotelBookings
            .Include(item => item.Pet)
            .Include(item => item.FoodPlan)
            .FirstOrDefaultAsync(item =>
                item.HotelBookingId == bookingId &&
                item.CustomerId == customerId);
        if (booking == null)
        {
            return HotelCommandResult.Fail(
                "Không tìm thấy lịch đặt phòng hoặc bạn không có quyền hủy.");
        }

        // [nam][BR] Chỉ booking chưa check-in mới được hủy online.
        if (!string.Equals(booking.Status, "Đã đặt", StringComparison.OrdinalIgnoreCase))
        {
            return HotelCommandResult.Fail(
                "Chỉ có thể hủy lịch đang ở trạng thái Đã đặt.");
        }

        // [nam][BR] Khách phải hủy trước giờ nhận ít nhất một giờ; sát giờ cần Staff xử lý trực tiếp.
        DateTime scheduledCheckIn = booking.ScheduledCheckInDate ?? booking.CheckInDate;
        if (scheduledCheckIn <= DateTime.Now.AddHours(1))
        {
            return HotelCommandResult.Fail(
                "Chỉ có thể hủy online trước giờ nhận phòng ít nhất 1 giờ. " +
                "Vui lòng liên hệ cửa hàng để được hỗ trợ.");
        }

        await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        try
        {
            // [nam][Flow] Chỉ hoàn đúng lượng đã trừ và đặt về 0 để thao tác lặp không hoàn kho hai lần.
            if (booking.FoodPlan?.ProductSku != null && booking.FoodPlan.InventoryQuantityDeducted > 0)
            {
                var stockDetails = new List<StockMovementDetail>
                {
                    new()
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
                    details: stockDetails);
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
        catch (ServiceException ex)
        {
            await transaction.RollbackAsync();
            return HotelCommandResult.Fail(ex.Message);
        }

        return HotelCommandResult.Ok($"Đã hủy lịch đặt chuồng của {booking.Pet.Name}.");
    }
}
