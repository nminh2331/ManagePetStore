namespace ManagePetStore.Areas.Customer.Models;

public class PendingRegistration
{
    public string FullName { get; set; } = "";
    public string Email { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Password { get; set; } = "";
    public string OtpCode { get; set; } = "";
    public DateTime ExpiresAt { get; set; }
}

public class VerifyOtpViewModel
{
    public string Email { get; set; } = "";
    public string OtpCode { get; set; } = "";
}
