namespace ManagePetStore.Areas.Customer.Models;

public class PendingPasswordReset
{
    public string Email { get; set; } = "";
    public int UserId { get; set; }
    public string FullName { get; set; } = "";
    public string OtpCode { get; set; } = "";
    public DateTime ExpiresAt { get; set; }
    public bool OtpVerified { get; set; }
}

public class ForgotPasswordViewModel
{
    public string Email { get; set; } = "";
}

public class ForgotPasswordVerifyOtpViewModel
{
    public string Email { get; set; } = "";
    public string OtpCode { get; set; } = "";
}

public class ResetPasswordViewModel
{
    public string Email { get; set; } = "";
    public string NewPassword { get; set; } = "";
    public string ConfirmNewPassword { get; set; } = "";
}
