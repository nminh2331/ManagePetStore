namespace ManagePetStore.Models;

public class HotelCageStaySegment
{
    public int StaySegmentId { get; set; }
    public int HotelBookingId { get; set; }
    public string CageId { get; set; } = null!;
    public int RoomTypeId { get; set; }
    public decimal DailyPriceSnapshot { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public string StartReason { get; set; } = "CheckIn";
    public string? EndReason { get; set; }
    public DateTime CreatedAt { get; set; }

    public virtual HotelBooking HotelBooking { get; set; } = null!;
    public virtual Cage Cage { get; set; } = null!;
    public virtual RoomType RoomType { get; set; } = null!;
}
