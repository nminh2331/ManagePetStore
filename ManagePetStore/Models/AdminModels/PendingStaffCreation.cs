using System;

namespace ManagePetStore.Models.AdminModels
{
    public class PendingStaffCreation
    {
        public string FullName { get; set; } = "";
        public string Email { get; set; } = "";
        public string Phone { get; set; } = "";
        public int RoleId { get; set; }
        public string Password { get; set; } = "";
        public string OtpCode { get; set; } = "";
        public DateTime ExpiresAt { get; set; }
    }
}

