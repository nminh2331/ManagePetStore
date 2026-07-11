namespace ManagePetStore.Models;

public partial class FoodDiaryLog
{
    public string ActivityType { get; set; } = "General";

    public string Title { get; set; } = "Nhật ký chăm sóc";

    public string? MediaUrl { get; set; }

    public string? MediaType { get; set; }

    public bool IsVisibleToCustomer { get; set; } = true;

    public int? CreatedByUserId { get; set; }
}
