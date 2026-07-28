namespace ManagePetStore.Models;

public static class HotelPricingPolicy
{
    private const int HoursPerDay = 24;

    // [nam] Tính số ngày dịch vụ/thức ăn, luôn tối thiểu một ngày.
    public static int CalculateStayDays(DateTime checkInAt, DateTime checkOutAt)
    {
        return Math.Max(
            1,
            (int)Math.Ceiling(Math.Max(0, (checkOutAt - checkInAt).TotalHours) / HoursPerDay));
    }

    // [nam] Tiền phòng tính theo ngày đủ + giờ lẻ; tiền giờ không bao giờ vượt giá một ngày.
    public static HotelRoomCharge CalculateRoomCharge(
        DateTime checkInAt,
        DateTime checkOutAt,
        decimal dailyPrice,
        decimal hourlyPrice)
    {
        if (dailyPrice <= 0)
        {
            throw new InvalidOperationException("Giá phòng theo ngày không hợp lệ.");
        }

        if (hourlyPrice <= 0 || hourlyPrice > dailyPrice)
        {
            throw new InvalidOperationException("Giá phòng theo giờ không hợp lệ.");
        }

        double totalHours = Math.Max(0, (checkOutAt - checkInAt).TotalHours);
        if (totalHours <= HoursPerDay)
        {
            return new HotelRoomCharge(1, 0, dailyPrice, hourlyPrice, dailyPrice);
        }

        int fullDays = (int)Math.Floor(totalHours / HoursPerDay);
        double remainingHours = totalHours - fullDays * HoursPerDay;
        int extraHours = remainingHours > 0
            ? (int)Math.Ceiling(remainingHours)
            : 0;
        decimal extraAmount = Math.Min(extraHours * hourlyPrice, dailyPrice);

        // Khi phần giờ đã chạm trần giá ngày, hiển thị thành một ngày trọn vẹn.
        if (extraAmount >= dailyPrice)
        {
            return new HotelRoomCharge(fullDays + 1, 0, dailyPrice, hourlyPrice, (fullDays + 1) * dailyPrice);
        }

        return new HotelRoomCharge(
            fullDays,
            extraHours,
            dailyPrice,
            hourlyPrice,
            fullDays * dailyPrice + extraAmount);
    }

    // [nam] Quy đổi hạng thành viên thành tỷ lệ giảm giá tiền chuồng.
    public static decimal ResolveMembershipDiscountRate(string? membershipTier)
    {
        return membershipTier?.Trim().ToLowerInvariant() switch
        {
            "gold" or "vàng" => 0.10m,
            "silver" or "bạc" => 0.05m,
            _ => 0m
        };
    }

    // [nam] Tính số tiền giảm giá thành viên trên tổng tiền chuồng.
    public static decimal CalculateMembershipDiscount(decimal roomSubtotal, string? membershipTier)
    {
        return decimal.Round(
            Math.Max(0, roomSubtotal) * ResolveMembershipDiscountRate(membershipTier),
            0,
            MidpointRounding.AwayFromZero);
    }
}

public readonly record struct HotelRoomCharge(
    int FullDays,
    int ExtraHours,
    decimal DailyPrice,
    decimal HourlyPrice,
    decimal TotalAmount)
{
    public int ChargeableDayPeriods => FullDays + (ExtraHours > 0 ? 1 : 0);

    public string DurationText => ExtraHours > 0
        ? $"{FullDays} ngày + {ExtraHours} giờ"
        : $"{FullDays} ngày";
}
