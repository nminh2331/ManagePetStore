namespace ManagePetStore.Areas.ServiceStaff.Models;

public class PetDailyCareListViewModel
{
    public string SearchTerm { get; set; } = string.Empty;
    public List<PetDailyCarePetRowViewModel> Pets { get; set; } = [];
}

public class PetDailyCarePetRowViewModel
{
    public int PetId { get; set; }
    public string PetName { get; set; } = string.Empty;
    public string Species { get; set; } = string.Empty;
    public string? Breed { get; set; }
    public string? ImageUrl { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public int HotelBookingId { get; set; }
    public string DisplayBookingId => $"HB{HotelBookingId:0000}";
    public string CageId { get; set; } = string.Empty;
    public string RoomTypeName { get; set; } = string.Empty;
    public DateTime CheckInAt { get; set; }
    public DateTime? ExpectedCheckOutAt { get; set; }
    public int CareLogCount { get; set; }
    public DateTime? LastCareAt { get; set; }
}

public class PetDailyCareDetailsViewModel
{
    public int PetId { get; set; }
    public string PetName { get; set; } = string.Empty;
    public string Species { get; set; } = string.Empty;
    public string? Breed { get; set; }
    public string? Age { get; set; }
    public decimal Weight { get; set; }
    public string? Pathology { get; set; }
    public string? ImageUrl { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public string? CustomerEmail { get; set; }
    public string SelectedTab { get; set; } = "current";
    public PetDailyCareStayViewModel? CurrentStay { get; set; }
    public List<PetDailyCareStayViewModel> Stays { get; set; } = [];
    public List<PetDailyCareLogViewModel> CurrentLogs { get; set; } = [];
    public List<PetDailyCareLogViewModel> AllLogs { get; set; } = [];
}

public class PetDailyCareStayViewModel
{
    public int HotelBookingId { get; set; }
    public string DisplayBookingId => $"HB{HotelBookingId:0000}";
    public string CageId { get; set; } = string.Empty;
    public string RoomTypeName { get; set; } = string.Empty;
    public DateTime CheckInAt { get; set; }
    public DateTime? CheckOutAt { get; set; }
    public string Status { get; set; } = string.Empty;
    public string StatusKey { get; set; } = string.Empty;
    public string FoodPlanName { get; set; } = "Chủ nuôi tự chuẩn bị";
    public int PortionGrams { get; set; }
    public int MealsPerDay { get; set; }
}

public class PetDailyCareLogViewModel
{
    public string LogId { get; set; } = string.Empty;
    public int HotelBookingId { get; set; }
    public string DisplayBookingId => $"HB{HotelBookingId:0000}";
    public DateTime? OccurredAt { get; set; }
    public string LegacyTime { get; set; } = string.Empty;
    public string ActivityType { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string FoodType { get; set; } = string.Empty;
    public string Amount { get; set; } = string.Empty;
    public string? MealType { get; set; }
    public int? ConsumedPercent { get; set; }
    public bool IsExtraCharge { get; set; }
    public decimal ExtraChargeAmount { get; set; }
    public string StaffName { get; set; } = string.Empty;
    public string? Note { get; set; }
    public string? MediaUrl { get; set; }
    public string? MediaType { get; set; }
    public bool IsVisibleToCustomer { get; set; }
}
