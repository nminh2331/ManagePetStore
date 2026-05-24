using System;
using System.Collections.Generic;

namespace ManagePetStore.Model;

public partial class EmployeeSchedule
{
    public int ScheduleId { get; set; }

    public int UserId { get; set; }

    public DateOnly WorkDate { get; set; }

    public TimeOnly ShiftStart { get; set; }

    public TimeOnly ShiftEnd { get; set; }

    public bool? IsAvailable { get; set; }

    public virtual User User { get; set; } = null!;
}
