namespace ManagePetStore.Models;

public static class HotelFoodPricing
{
    public const decimal SmallPetMultiplier = 1.00m;
    public const decimal MediumPetMultiplier = 1.25m;
    public const decimal LargePetMultiplier = 1.50m;
    public const decimal ExtraLargePetMultiplier = 1.80m;
    public const decimal PriceRoundingStep = 1000m;

    // [nam] Tính đơn giá, số suất kho và tổng tiền thức ăn theo cân nặng của pet.
    public static HotelFoodPriceQuote Calculate(decimal basePricePerDay, decimal petWeightKg, int chargeableDays)
    {
        if (basePricePerDay <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(basePricePerDay), "Giá gói thức ăn mỗi ngày phải lớn hơn 0.");
        }

        if (petWeightKg <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(petWeightKg), "Cân nặng thú cưng phải lớn hơn 0.");
        }

        if (chargeableDays <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(chargeableDays), "Số ngày tính phí phải lớn hơn 0.");
        }

        int days = chargeableDays;
        decimal normalizedWeight = decimal.Round(petWeightKg, 2, MidpointRounding.AwayFromZero);
        decimal multiplier = ResolvePortionMultiplier(normalizedWeight);
        decimal adjustedPrice = RoundUp(basePricePerDay * multiplier, PriceRoundingStep);

        return new HotelFoodPriceQuote(
            BasePricePerDay: basePricePerDay,
            PetWeightKg: normalizedWeight,
            PortionMultiplier: multiplier,
            PricePerDay: adjustedPrice,
            ChargeableDays: days,
            InventoryUnits: CalculateInventoryUnits(days, multiplier),
            TotalAmount: adjustedPrice * days,
            WeightBand: ResolveWeightBand(normalizedWeight));
    }

    // [nam] Chọn hệ số khẩu phần dựa trên nhóm cân nặng.
    public static decimal ResolvePortionMultiplier(decimal petWeightKg)
    {
        if (petWeightKg <= 5m) return SmallPetMultiplier;
        if (petWeightKg <= 15m) return MediumPetMultiplier;
        if (petWeightKg <= 30m) return LargePetMultiplier;
        return ExtraLargePetMultiplier;
    }

    // [nam] Quy đổi số ngày và hệ số khẩu phần thành số đơn vị cần trừ kho.
    public static int CalculateInventoryUnits(int chargeableDays, decimal portionMultiplier)
    {
        int days = Math.Max(0, chargeableDays);
        decimal multiplier = portionMultiplier > 0 ? portionMultiplier : SmallPetMultiplier;
        return (int)decimal.Ceiling(days * multiplier);
    }

    // [nam] Trả về nhãn nhóm cân nặng dùng trong bảng giá và giao diện.
    public static string ResolveWeightBand(decimal petWeightKg)
    {
        if (petWeightKg <= 5m) return "Nhỏ (≤5kg)";
        if (petWeightKg <= 15m) return "Trung bình (>5–15kg)";
        if (petWeightKg <= 30m) return "Lớn (>15–30kg)";
        return "Rất lớn (>30kg)";
    }

    // [nam] Làm tròn chi phí lên bội số giá quy định.
    private static decimal RoundUp(decimal value, decimal step)
    {
        if (value <= 0 || step <= 0) return Math.Max(0, value);
        return decimal.Ceiling(value / step) * step;
    }
}

public sealed record HotelFoodPriceQuote(
    decimal BasePricePerDay,
    decimal PetWeightKg,
    decimal PortionMultiplier,
    decimal PricePerDay,
    int ChargeableDays,
    int InventoryUnits,
    decimal TotalAmount,
    string WeightBand);
