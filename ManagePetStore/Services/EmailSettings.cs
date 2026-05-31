namespace ManagePetStore.Services;

public class EmailSettings
{
    public string SenderName { get; set; } = "PetStore";
    public string SenderEmail { get; set; } = "";
    public string Password { get; set; } = "";
    public string SmtpServer { get; set; } = "smtp.gmail.com";
    public int Port { get; set; } = 587;
}
