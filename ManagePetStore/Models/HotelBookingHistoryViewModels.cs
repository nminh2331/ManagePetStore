namespace ManagePetStore.Models;

public class HotelBookingHistoryDetailViewModel
{
    public int HotelBookingId { get; set; }
    public string DisplayBookingId => $"HB{HotelBookingId:0000}";
    public string Status { get; set; } = string.Empty;
    public string StatusKey { get; set; } = string.Empty;
    public DateTime CheckInDate { get; set; }
    public DateTime? CheckOutDate { get; set; }
    public DateTime? ScheduledCheckInDate { get; set; }
    public DateTime? ScheduledCheckOutDate { get; set; }
    public DateTime? ActualCheckInAt { get; set; }
    public DateTime? ActualCheckOutAt { get; set; }
    public int StayDays { get; set; }
    public decimal BaseDailyPrice { get; set; }
    public decimal Subtotal { get; set; }
    public decimal Discount { get; set; }
    public decimal FinalAmount { get; set; }
    public int EarnedPoints { get; set; }
    public int PetId { get; set; }
    public string PetName { get; set; } = string.Empty;
    public string PetSpecies { get; set; } = string.Empty;
    public string? PetBreed { get; set; }
    public string? PetAge { get; set; }
    public decimal PetWeight { get; set; }
    public string? PetPathology { get; set; }
    public string? PetImageUrl { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public string? CustomerEmail { get; set; }
    public string CageId { get; set; } = string.Empty;
    public string RoomTypeName { get; set; } = string.Empty;
    public string RoomSize { get; set; } = string.Empty;
    public bool HasAc { get; set; }
    public bool HasCamera { get; set; }
    public bool HasPremiumFood { get; set; }
    public string FoodPlanName { get; set; } = "Chưa ghi nhận gói ăn";
    public string? FoodProductSku { get; set; }
    public string FoodProductUnit { get; set; } = HotelFoodCatalog.DailyUnit;
    public decimal FoodPricePerDay { get; set; }
    public int FoodPortionGrams { get; set; }
    public int FoodMealsPerDay { get; set; }
    public string? FeedingInstructions { get; set; }
    public string? FoodAllergyNotes { get; set; }
    public List<HotelBookingAddonHistoryItem> Addons { get; set; } = [];
    public List<HotelBookingTimelineHistoryItem> Timeline { get; set; } = [];
    public List<HotelBookingMedicalHistoryItem> MedicalRecords { get; set; } = [];
    public List<HotelBookingCareHistoryItem> CareLogs { get; set; } = [];
}

public class HotelBookingAddonHistoryItem
{
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
}

public class HotelBookingTimelineHistoryItem
{
    public DateTime OccurredAt { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

public class HotelBookingMedicalHistoryItem
{
    public int RecordId { get; set; }
    public DateTime DateCreated { get; set; }
    public decimal Weight { get; set; }
    public string HealthStatus { get; set; } = string.Empty;
    public string? Symptoms { get; set; }
    public string? Treatment { get; set; }
    public string? VaccinationStatus { get; set; }
    public string? ParasitePrevention { get; set; }
    public string? PhysicalCheck { get; set; }
}

public class HotelBookingCareHistoryItem
{
    public string LogId { get; set; } = string.Empty;
    public DateTime? OccurredAt { get; set; }
    public string LegacyTime { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string ActivityType { get; set; } = "General";
    public string Title { get; set; } = "Nhật ký chăm sóc";
    public string FoodType { get; set; } = string.Empty;
    public string Amount { get; set; } = string.Empty;
    public string? PhotoUrl { get; set; }
    public string? MediaUrl { get; set; }
    public string? MediaType { get; set; }
    public string? MealType { get; set; }
    public decimal? ServedGrams { get; set; }
    public int? ConsumedPercent { get; set; }
    public bool IsExtraCharge { get; set; }
    public decimal ExtraChargeAmount { get; set; }
    public string? Note { get; set; }
    public string StaffName { get; set; } = string.Empty;
}
