using System;
using System.Collections.Generic;

namespace ManagePetStore.Models;

public partial class StaffTask
{
    public string TaskId { get; set; } = null!;

    public int PetId { get; set; }

    public int CustomerId { get; set; }

    public int AssignedStaffId { get; set; }

    public string ServiceDescription { get; set; } = null!;

    public string? ScheduledTime { get; set; }

    public string? Location { get; set; }

    public string? Notes { get; set; }

    public string Status { get; set; } = null!;

    public string Priority { get; set; } = null!;

    public virtual User AssignedStaff { get; set; } = null!;

    public virtual Customer Customer { get; set; } = null!;

    public virtual Pet Pet { get; set; } = null!;
}
