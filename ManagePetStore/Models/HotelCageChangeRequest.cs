namespace ManagePetStore.Models;

public class HotelCageChangeRequest
{
    public int ChangeRequestId { get; set; }
    public int HotelBookingId { get; set; }
    public int CustomerId { get; set; }
    public string SourceCageId { get; set; } = null!;
    public string TargetCageId { get; set; } = null!;
    public string Reason { get; set; } = null!;
    public string Status { get; set; } = "Pending";
    public int RemainingDaysSnapshot { get; set; }
    public decimal SourceDailyPriceSnapshot { get; set; }
    public decimal TargetDailyPriceSnapshot { get; set; }
    public decimal PriceDifferenceSnapshot { get; set; }
    public DateTime RequestedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public int? ProcessedByUserId { get; set; }
    public string? ProcessedByName { get; set; }
    public string? DecisionNote { get; set; }
    public DateTime? AppliedAt { get; set; }

    public virtual HotelBooking HotelBooking { get; set; } = null!;
    public virtual Customer Customer { get; set; } = null!;
    public virtual Cage SourceCage { get; set; } = null!;
    public virtual Cage TargetCage { get; set; } = null!;
    public virtual User? ProcessedByUser { get; set; }
}
