namespace ManagePetStore.Models;

public static class HotelPricingPolicy
{
    public static int CalculateStayDays(DateTime checkInAt, DateTime checkOutAt)
    {
        return Math.Max(
            1,
            (int)Math.Ceiling(Math.Max(0, (checkOutAt - checkInAt).TotalHours) / 24d));
    }

    public static decimal ResolveMembershipDiscountRate(string? membershipTier)
    {
        return membershipTier?.Trim().ToLowerInvariant() switch
        {
            "gold" or "vàng" => 0.10m,
            "silver" or "bạc" => 0.05m,
            _ => 0m
        };
    }

    public static decimal CalculateMembershipDiscount(decimal roomSubtotal, string? membershipTier)
    {
        return decimal.Round(
            Math.Max(0, roomSubtotal) * ResolveMembershipDiscountRate(membershipTier),
            0,
            MidpointRounding.AwayFromZero);
    }
}
