namespace ManagePetStore.Areas.Customer.Models;

public class CustomerNotificationItemViewModel
{
    public long NotificationId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public bool IsRead { get; set; }
    public string DisplayTitle => ManagePetStore.Models.CageTerminology.ForDisplay(Title);
    public string DisplayMessage => ManagePetStore.Models.CageTerminology.ForDisplay(Message);
}

public class CustomerNotificationMenuViewModel
{
    public bool IsCustomer { get; set; }
    public int UnreadCount { get; set; }
    public List<CustomerNotificationItemViewModel> Items { get; set; } = [];
}

public class CustomerNotificationPageViewModel : CustomerSidebarViewModel
{
    public List<CustomerNotificationItemViewModel> Notifications { get; set; } = [];
    public int UnreadCount { get; set; }
}
