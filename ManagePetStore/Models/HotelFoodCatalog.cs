namespace ManagePetStore.Models;

public static class HotelFoodCatalog
{
    public const string CategoryCode = "HOTEL_STAY_FOOD";
    public const string CategoryName = "Thức ăn cho Pet lưu trú";
    public const string DailyUnit = "Suất/ngày";
    public const string ProductSkuPrefix = "HOTEL-FOOD-";

    public static bool IsSpeciesCompatible(string? targetSpecies, string petSpecies)
    {
        return string.IsNullOrWhiteSpace(targetSpecies) ||
               string.Equals(targetSpecies, "Tất cả", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(targetSpecies, petSpecies, StringComparison.OrdinalIgnoreCase);
    }
}
