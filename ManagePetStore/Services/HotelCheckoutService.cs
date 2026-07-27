using System.Data;
using ManagePetStore.Areas.ServiceStaff.Models;
using ManagePetStore.Models;
using ManagePetStore.Services.Warehouse;
using Microsoft.EntityFrameworkCore;

namespace ManagePetStore.Services;

public class HotelCheckoutService : IHotelCheckoutService
{
    private static readonly TimeSpan DefaultScheduledCheckoutTime = TimeSpan.FromHours(12);
    private static readonly TimeSpan LateCheckoutGracePeriod = TimeSpan.FromMinutes(30);
    private const int HoursPerLateDay = 24;

    private readonly PetStoreManagementContext _context;
    private readonly IInventoryBatchService _inventoryBatchService;

    // [nam] Khởi tạo dịch vụ checkout và thành phần đối soát tồn kho thức ăn.
    public HotelCheckoutService(
        PetStoreManagementContext context,
        IInventoryBatchService inventoryBatchService)
    {
        _context = context;
        _inventoryBatchService = inventoryBatchService;
    }

    // [nam] Tải booking và lập bảng tính checkout tạm thời mà không thay đổi dữ liệu.
    public async Task<HotelCheckoutPreviewViewModel?> GetPreviewAsync(int bookingId, DateTime? checkoutAt = null)
    {
        var booking = await LoadBookingAsync(bookingId, true);
        if (booking == null)
        {
            return null;
        }

        ValidateCheckoutPricing(booking);
        return BuildPreview(booking, checkoutAt ?? booking.CheckoutStatement?.CheckoutAt ?? DateTime.Now);
    }

    // [nam] Chốt bảng kê checkout, đối soát tồn kho và chuyển booking sang chờ thanh toán.
    public async Task<HotelCheckoutPreviewViewModel> PrepareAsync(
        PrepareHotelCheckoutRequest request,
        int? staffUserId,
        string staffName)
    {
        // [nam][BR] Chốt bảng kê và đối soát kho trong cùng transaction để số tiền luôn khớp tài nguyên thực tế.
        await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var booking = await LoadBookingAsync(request.HotelBookingId, false)
            ?? throw new InvalidOperationException("Không tìm thấy lượt đặt chuồng.");

        // [nam][Validate] Chỉ lượt đang lưu trú được gửi sang thu ngân; booking đặt trước chưa phát sinh checkout.
        if (booking.Status != "Active" && booking.Status != "Đang ở")
        {
            throw new InvalidOperationException("Chỉ có thể chốt chi phí cho pet đang lưu trú.");
        }

        // [nam][BR] Idempotent: bấm chốt lặp hoặc tab cũ không được tạo bảng kê/đơn thanh toán thứ hai.
        bool checkoutAlreadyPrepared = booking.CheckoutStatement != null &&
            (string.Equals(booking.CheckoutStatement.Status, "ReadyForPayment", StringComparison.OrdinalIgnoreCase) ||
             booking.CheckoutStatement.OrderId != null);
        if (checkoutAlreadyPrepared)
        {
            // Idempotent: a stale browser click must not create or update the checkout again.
            return BuildPreview(
                booking,
                booking.CheckoutStatement!.CheckoutAt);
        }

        // [nam][Validate] Kiểm tra lại giá snapshot và khoản phát sinh ở service, không chỉ dựa vào ModelState/UI.
        ValidateCheckoutInputs(booking, request);

        var checkoutAt = DateTime.Now;
        // [nam][Flow] Chốt số ngày thực tế trước, đối soát kho, sau đó mới dựng các dòng tiền của bảng kê.
        var chargeableFoodDays = ResolveFoodDays(booking, checkoutAt);
        await ReconcileFoodInventoryAsync(booking, chargeableFoodDays);
        var preview = BuildPreview(booking, checkoutAt, request.OtherDescription, request.OtherAmount);
        var statement = booking.CheckoutStatement ?? new HotelCheckoutStatement
        {
            HotelBookingId = booking.HotelBookingId
        };

        // [nam][Flow] Bảng kê cũ ở Draft được thay toàn bộ item để không cộng dồn khoản phí khi Staff chốt lại.
        if (booking.CheckoutStatement == null)
        {
            _context.HotelCheckoutStatements.Add(statement);
        }
        else
        {
            _context.HotelCheckoutItems.RemoveRange(statement.Items);
        }

        statement.Status = "ReadyForPayment";
        statement.CheckoutAt = checkoutAt;
        statement.RoomAmount = preview.RoomAmount;
        statement.FoodAmount = preview.FoodAmount;
        statement.AddonAmount = preview.AddonAmount;
        statement.LateFeeAmount = preview.LateFeeAmount;
        statement.OtherAmount = preview.OtherAmount;
        statement.DiscountAmount = preview.DiscountAmount;
        statement.TotalAmount = preview.TotalAmount;
        statement.PreparedByUserId = staffUserId;
        statement.PreparedByName = staffName;
        statement.PreparedAt = DateTime.Now;
        statement.Items = preview.Items.Select(item => new HotelCheckoutItem
        {
            ChargeType = item.ChargeType,
            Description = item.Description,
            Quantity = item.Quantity,
            Unit = item.Unit,
            UnitPrice = item.UnitPrice,
            Amount = item.Amount
        }).ToList();

        // [nam][BR] TotalAmount của gói chỉ chứa tiền theo ngày; bữa ngoài gói là item riêng để tránh tính hai lần.
        if (booking.FoodPlan != null)
        {
            var extraFoodAmount = booking.FoodDiaryLogs
                .Where(log => log.IsExtraCharge && log.ExtraChargeAmount > 0)
                .Sum(log => log.ExtraChargeAmount);
            booking.FoodPlan.ChargeableDays = chargeableFoodDays;
            booking.FoodPlan.TotalAmount = preview.FoodAmount - extraFoodAmount;
        }
        // [nam][BR] Lượt gửi trực tiếp không có lịch trả cố định được tính tiền phòng theo thời gian ở thực tế.
        if (!HasScheduledCheckout(booking))
        {
            var roomQuote = CalculateRoomCharge(booking, checkoutAt);
            booking.StayDays = roomQuote.ChargeableDays;
            booking.Subtotal = roomQuote.Amount;
        }
        booking.FinalAmount = preview.TotalAmount;
        _context.PetBioTimelines.Add(new PetBioTimeline
        {
            PetId = booking.PetId,
            HotelBookingId = booking.HotelBookingId,
            Date = DateTime.Now,
            Title = "Gửi bảng kê tạm tính",
            Type = "HotelCheckoutPrepared",
            Description = $"{staffName} đã lập bảng kê tạm tính {preview.TotalAmount:N0}đ và gửi sang quầy thu ngân. Pet vẫn đang lưu trú cho đến khi hoàn tất trả pet."
        });

        // [nam][BR] Chỉ ghép Spa đã hoàn thành, chưa thanh toán, cùng pet/customer và phát sinh trong lượt lưu trú.
        var completedSpaIds = await _context.SpaBookings
            .Where(spa => spa.PetId == booking.PetId &&
                          spa.CustomerId == booking.CustomerId &&
                          (spa.SpaStatus == "4" || spa.SpaStatus.EndsWith("|4")) &&
                          (spa.Status == "Chờ thanh toán" || spa.Status == "pending" || spa.Status == "Chưa thanh toán") &&
                          spa.DateTime >= booking.CheckInDate && spa.DateTime <= checkoutAt)
            .Select(spa => spa.BookingId)
            .ToListAsync();
        var linkedSpaIds = await _context.HotelStaySpaLinks
            .Where(link => completedSpaIds.Contains(link.SpaBookingId))
            .Select(link => link.SpaBookingId)
            .ToListAsync();
        foreach (var spaId in completedSpaIds.Except(linkedSpaIds))
        {
            _context.HotelStaySpaLinks.Add(new HotelStaySpaLink
            {
                HotelBookingId = booking.HotelBookingId,
                SpaBookingId = spaId,
                LinkedAt = DateTime.Now
            });
        }

        await _context.SaveChangesAsync();
        await transaction.CommitAsync();
        return await GetPreviewAsync(booking.HotelBookingId) ?? preview;
    }

    // [nam] Khấu trừ hoặc hoàn kho phần thức ăn chênh lệch theo số ngày thực tế.
    private async Task ReconcileFoodInventoryAsync(HotelBooking booking, int chargeableFoodDays)
    {
        var foodPlan = booking.FoodPlan;
        if (foodPlan == null || string.IsNullOrWhiteSpace(foodPlan.ProductSku))
        {
            return;
        }

        // [nam][BR] Chỉ trừ/hoàn phần chênh lệch giữa suất cần theo ngày thực tế và suất đã giữ trước đó.
        int requiredInventoryUnits = HotelFoodPricing.CalculateInventoryUnits(
            chargeableFoodDays,
            foodPlan.PortionMultiplierSnapshot);
        int difference = requiredInventoryUnits - foodPlan.InventoryQuantityDeducted;
        try
        {
            if (difference > 0)
            {
                await _inventoryBatchService.DeductStockFIFO(foodPlan.ProductSku, difference);
            }
            else if (difference < 0)
            {
                await _inventoryBatchService.RestockToBatches(foodPlan.ProductSku, -difference);
            }
        }
        catch (ManagePetStore.Exceptions.ServiceException ex)
        {
            throw new InvalidOperationException(
                $"Không thể đối soát tồn kho gói ăn khi checkout: {ex.Message}",
                ex);
        }

        foodPlan.InventoryQuantityDeducted = requiredInventoryUnits;
    }

    // [nam] Tải đầy đủ dữ liệu liên quan cần cho phép tính và lưu checkout.
    private async Task<HotelBooking?> LoadBookingAsync(int bookingId, bool noTracking)
    {
        var query = _context.HotelBookings
            .Include(booking => booking.Pet)
            .Include(booking => booking.Cage).ThenInclude(cage => cage.RoomType)
            .Include(booking => booking.BookingAddons)
            .Include(booking => booking.FoodPlan)
            .Include(booking => booking.FoodDiaryLogs)
            .Include(booking => booking.CheckoutStatement).ThenInclude(statement => statement!.Items)
            .Include(booking => booking.CheckoutStatement).ThenInclude(statement => statement!.Order)
            .Where(booking => booking.HotelBookingId == bookingId);
        return await (noTracking ? query.AsNoTracking() : query).FirstOrDefaultAsync();
    }

    // [nam] Tổng hợp tiền chuồng, thức ăn, dịch vụ, phí trễ và khoản phát sinh.
    private static HotelCheckoutPreviewViewModel BuildPreview(
        HotelBooking booking,
        DateTime checkoutAt,
        string? otherDescription = null,
        decimal? requestedOtherAmount = null)
    {
        // [nam][Flow] Tổng tiền gồm phòng - giảm giá + gói ăn + bữa ngoài gói + addon + trả muộn + phí khác.
        var foodDays = ResolveFoodDays(booking, checkoutAt);
        var planFoodAmount = (booking.FoodPlan?.PricePerDaySnapshot ?? 0) * foodDays;
        var extraFoodLogs = booking.FoodDiaryLogs.Where(log => log.IsExtraCharge && log.ExtraChargeAmount > 0).ToList();
        var extraFoodAmount = extraFoodLogs.Sum(log => log.ExtraChargeAmount);
        var addonAmount = booking.BookingAddons.Sum(addon => addon.Price);
        var lateFeeQuote = CalculateLateFee(booking, checkoutAt);
        var lateFee = lateFeeQuote.Amount;
        var savedOtherItem = booking.CheckoutStatement?.Items.FirstOrDefault(item => item.ChargeType == "Other");
        var otherAmount = requestedOtherAmount ?? savedOtherItem?.Amount ?? 0;
        var description = string.IsNullOrWhiteSpace(otherDescription) ? savedOtherItem?.Description : otherDescription.Trim();
        var roomQuote = CalculateRoomCharge(booking, checkoutAt);
        var roomAmount = roomQuote.Amount;
        // [nam][BR] Hóa đơn không được âm kể cả dữ liệu giảm giá cũ lớn hơn các khoản còn lại.
        var total = Math.Max(0, roomAmount - booking.Discount + planFoodAmount + extraFoodAmount + addonAmount + lateFee + otherAmount);

        var items = new List<HotelCheckoutPreviewItem>
        {
            new()
            {
                ChargeType = "Room",
                Description = $"Phòng {booking.Cage.RoomType.Type} · chuồng {booking.CageId}",
                Quantity = roomQuote.ChargeableDays,
                Unit = "ngày",
                UnitPrice = roomQuote.UnitPrice,
                Amount = roomAmount
            }
        };
        if (planFoodAmount > 0)
        {
            string weightDetail = booking.FoodPlan!.PetWeightSnapshot.HasValue
                ? $" · {booking.FoodPlan.PetWeightSnapshot:0.##}kg · hệ số {booking.FoodPlan.PortionMultiplierSnapshot:0.##}"
                : " · dữ liệu cũ chưa có cân nặng snapshot";
            items.Add(new()
            {
                ChargeType = "FoodPlan",
                Description = CageTerminology.ForDisplay(booking.FoodPlan.FoodNameSnapshot) + weightDetail,
                Quantity = foodDays,
                Unit = "ngày",
                UnitPrice = booking.FoodPlan.PricePerDaySnapshot,
                Amount = planFoodAmount
            });
        }
        items.AddRange(booking.BookingAddons.Select(addon => new HotelCheckoutPreviewItem { ChargeType = "Addon", Description = CageTerminology.ForDisplay(addon.Name), Quantity = 1, Unit = "lần", UnitPrice = addon.Price, Amount = addon.Price }));
        items.AddRange(extraFoodLogs.Select(log => new HotelCheckoutPreviewItem { ChargeType = "ExtraFood", Description = $"Bữa phát sinh: {log.FoodType}", Quantity = 1, Unit = "lần", UnitPrice = log.ExtraChargeAmount, Amount = log.ExtraChargeAmount }));
        if (lateFee > 0)
        {
            items.Add(new()
            {
                ChargeType = "LateFee",
                Description = $"Phụ phí trả chuồng trễ {lateFeeQuote.ChargeableHours} giờ sau 30 phút miễn phí",
                Quantity = 1,
                Unit = "lần",
                UnitPrice = lateFee,
                Amount = lateFee
            });
        }
        if (otherAmount > 0) items.Add(new() { ChargeType = "Other", Description = description ?? "Chi phí phát sinh khác", Quantity = 1, Unit = "lần", UnitPrice = otherAmount, Amount = otherAmount });

        var orderStatus = booking.CheckoutStatement?.Order?.Status;
        return new HotelCheckoutPreviewViewModel
        {
            HotelBookingId = booking.HotelBookingId,
            PetName = booking.Pet.Name,
            CageId = booking.CageId,
            CheckoutAt = checkoutAt,
            RoomAmount = roomAmount,
            FoodAmount = planFoodAmount + extraFoodAmount,
            AddonAmount = addonAmount,
            LateFeeAmount = lateFee,
            OtherAmount = otherAmount,
            DiscountAmount = booking.Discount,
            TotalAmount = total,
            StatementStatus = booking.CheckoutStatement?.Status ?? "Draft",
            OrderId = booking.CheckoutStatement?.OrderId,
            OrderStatus = orderStatus,
            CanFinalize = HotelCheckoutWorkflow.CanFinalize(booking.CheckoutStatement?.OrderId, orderStatus),
            CanReset = HotelCheckoutWorkflow.CanReset(booking.CheckoutStatement),
            Items = items
        };
    }

    // [nam] Tính số ngày thức ăn cần thu theo thời gian lưu trú thực tế.
    private static int ResolveFoodDays(HotelBooking booking, DateTime checkoutAt)
    {
        // [nam][BR] Gói không có giá không phát sinh tiền; ngày ăn bắt đầu từ check-in thực tế nếu đã ghi nhận.
        if (booking.FoodPlan == null || booking.FoodPlan.PricePerDaySnapshot <= 0) return 0;
        var start = booking.ActualCheckInAt ?? booking.CheckInDate;
        return HotelPricingPolicy.CalculateStayDays(start, checkoutAt);
    }

    // [nam] Xác định booking có lưu mốc trả chuồng dự kiến hay không.
    private static bool HasScheduledCheckout(HotelBooking booking) =>
        booking.ScheduledCheckOutDate.HasValue || booking.CheckOutDate.HasValue;

    // [nam] Tính tiền chuồng thực tế theo thời điểm checkout và chính sách giá lưu trú.
    private static RoomChargeQuote CalculateRoomCharge(HotelBooking booking, DateTime checkoutAt)
    {
        // [nam][BR] Booking có lịch dự kiến giữ nguyên số ngày và giá đã chốt lúc đặt; phí trễ được tính riêng.
        if (HasScheduledCheckout(booking))
        {
            int bookedDays = Math.Max(booking.StayDays, 1);
            decimal savedAmount = Math.Max(0, booking.Subtotal);
            decimal effectiveUnitPrice = bookedDays > 0
                ? decimal.Round(savedAmount / bookedDays, 2, MidpointRounding.AwayFromZero)
                : booking.BaseDailyPrice;
            return new RoomChargeQuote(bookedDays, effectiveUnitPrice, savedAmount);
        }

        // [nam][BR] Lượt gửi trực tiếp tính ceil theo mỗi 24 giờ thực tế, tối thiểu một ngày theo pricing policy.
        var start = booking.ActualCheckInAt ?? booking.CheckInDate;
        int actualDays = HotelPricingPolicy.CalculateStayDays(start, checkoutAt);
        return new RoomChargeQuote(
            actualDays,
            booking.BaseDailyPrice,
            booking.BaseDailyPrice * actualDays);
    }

    // [nam] Tính số giờ và tiền phụ thu khi trả pet sau thời gian miễn phí.
    private static LateFeeQuote CalculateLateFee(HotelBooking booking, DateTime checkoutAt)
    {
        // [nam][BR] Dữ liệu cũ chỉ có ngày trả (00:00) được quy ước trả lúc 12:00.
        var scheduled = booking.ScheduledCheckOutDate ?? booking.CheckOutDate;
        if (scheduled.HasValue && scheduled.Value.TimeOfDay == TimeSpan.Zero)
            scheduled = scheduled.Value.Date.Add(DefaultScheduledCheckoutTime);

        // [nam][BR] Miễn phí 30 phút đầu; không có lịch trả thì đã tính theo ngày thực tế nên không cộng phí trễ.
        if (!scheduled.HasValue || checkoutAt <= scheduled.Value.Add(LateCheckoutGracePeriod))
        {
            return LateFeeQuote.None;
        }

        if (booking.Cage.RoomType.HourlyPrice <= 0)
        {
            throw new InvalidOperationException("Giá theo giờ của loại chuồng không hợp lệ nên chưa thể tính phí trả muộn.");
        }

        // [nam][BR] Mỗi 24 giờ trễ tính một ngày; phần giờ lẻ bị chặn tối đa bằng giá một ngày.
        var chargeableHours = (int)Math.Ceiling(
            (checkoutAt - scheduled.Value.Add(LateCheckoutGracePeriod)).TotalHours);
        var fullDays = chargeableHours / HoursPerLateDay;
        var remainingHours = chargeableHours % HoursPerLateDay;
        var amount = fullDays * booking.BaseDailyPrice
            + Math.Min(remainingHours * booking.Cage.RoomType.HourlyPrice, booking.BaseDailyPrice);

        return new LateFeeQuote(chargeableHours, amount);
    }

    // [nam] Kiểm tra giá booking và khoản chi phí phát sinh trước khi lập bảng kê.
    private static void ValidateCheckoutInputs(HotelBooking booking, PrepareHotelCheckoutRequest request)
    {
        ValidateCheckoutPricing(booking);

        // [nam][Validate] Phí nhập tay giới hạn 100 triệu, bắt buộc mô tả và dùng bước 1.000đ để dễ đối soát.
        if (request.OtherAmount < 0 || request.OtherAmount > 100000000m)
        {
            throw new InvalidOperationException("Chi phí phát sinh phải từ 0 đến 100.000.000đ.");
        }

        if (request.OtherAmount > 0 && string.IsNullOrWhiteSpace(request.OtherDescription))
        {
            throw new InvalidOperationException("Phải nhập mô tả khi có chi phí phát sinh.");
        }

        if (request.OtherAmount % 1000m != 0)
        {
            throw new InvalidOperationException("Chi phí phát sinh phải theo bước 1.000đ.");
        }
    }

    // [nam] Ngăn checkout khi dữ liệu giá phòng, giảm giá hoặc dịch vụ không hợp lệ.
    private static void ValidateCheckoutPricing(HotelBooking booking)
    {
        // [nam][Validate] Không cho chốt nếu snapshot giá phòng thiếu, âm hoặc booking có lịch nhưng subtotal bằng 0.
        if (booking.BaseDailyPrice <= 0 ||
            booking.Subtotal < 0 ||
            (HasScheduledCheckout(booking) && booking.Subtotal == 0))
        {
            throw new InvalidOperationException("Giá phòng của booking không hợp lệ. Vui lòng kiểm tra lại trước khi checkout.");
        }

        // [nam][Validate] Giảm giá không được âm hoặc vượt tiền phòng; mọi dịch vụ thành phần cũng không được âm.
        if (booking.Discount < 0 || booking.Discount > booking.Subtotal)
        {
            throw new InvalidOperationException("Số tiền giảm giá của booking không hợp lệ.");
        }

        if (booking.FoodPlan?.PricePerDaySnapshot < 0 || booking.BookingAddons.Any(addon => addon.Price < 0))
        {
            throw new InvalidOperationException("Booking có khoản phí âm. Vui lòng kiểm tra dữ liệu trước khi checkout.");
        }
    }

    // [nam] Kết quả nội bộ của phép tính phí trả muộn, tách số giờ và thành tiền.
    private readonly record struct LateFeeQuote(int ChargeableHours, decimal Amount)
    {
        public static LateFeeQuote None => new(0, 0);
    }

    // [nam] Kết quả nội bộ của phép tính tiền chuồng theo số ngày, đơn giá và tổng tiền.
    private readonly record struct RoomChargeQuote(int ChargeableDays, decimal UnitPrice, decimal Amount);
}
