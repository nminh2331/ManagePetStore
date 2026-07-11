namespace ManagePetStore.Models;

public partial class HotelBooking
{
    public virtual ICollection<CustomerNotification> CustomerNotifications { get; set; }
        = new List<CustomerNotification>();
}
