namespace ManagePetStore.Models;

public partial class Customer
{
    public virtual ICollection<CustomerNotification> CustomerNotifications { get; set; }
        = new List<CustomerNotification>();
}
