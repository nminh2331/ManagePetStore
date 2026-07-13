namespace ManagePetStore.Models;

public class HotelStaySpaLink
{
    public int HotelBookingId { get; set; }
    public int SpaBookingId { get; set; }
    public DateTime LinkedAt { get; set; }
    public virtual HotelBooking HotelBooking { get; set; } = null!;
    public virtual SpaBooking SpaBooking { get; set; } = null!;
}
