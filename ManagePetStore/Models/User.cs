using System;
using System.Collections.Generic;

namespace ManagePetStore.Models;

public partial class User
{
    public int UserId { get; set; }

    public string Password { get; set; } = null!;

    public string FullName { get; set; } = null!;

    public string? Email { get; set; }

    public string? Phone { get; set; }

    public int RoleId { get; set; }

    public string Status { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public string? AvatarPath { get; set; }

    public virtual ICollection<Blog> Blogs { get; set; } = new List<Blog>();

    public virtual Customer? Customer { get; set; }

    public virtual Role Role { get; set; } = null!;

    public virtual ICollection<RoomMaintenanceLog> RoomMaintenanceLogCreatedByUsers { get; set; } = new List<RoomMaintenanceLog>();

    public virtual ICollection<RoomMaintenanceLog> RoomMaintenanceLogEndedByUsers { get; set; } = new List<RoomMaintenanceLog>();

    public virtual ICollection<SpaBooking> SpaBookings { get; set; } = new List<SpaBooking>();

    public virtual StaffProfile? StaffProfile { get; set; }

    public virtual ICollection<StaffShift> StaffShifts { get; set; } = new List<StaffShift>();

    public virtual ICollection<StaffTask> StaffTasks { get; set; } = new List<StaffTask>();

    public virtual ICollection<StockMovement> StockMovements { get; set; } = new List<StockMovement>();
}
