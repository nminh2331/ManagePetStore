namespace ManagePetStore.Models;

public class CustomerNotification
{
    public long NotificationId { get; set; }

    public int CustomerId { get; set; }

    public int? HotelBookingId { get; set; }

    public string Type { get; set; } = "DailyCare";

    public string Title { get; set; } = null!;

    public string Message { get; set; } = null!;

    public string? LinkUrl { get; set; }

    public bool IsRead { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? ReadAt { get; set; }

    public virtual Customer Customer { get; set; } = null!;

    public virtual HotelBooking? HotelBooking { get; set; }
}
