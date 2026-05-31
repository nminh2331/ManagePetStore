using System;
using System.Collections.Generic;

namespace ManagePetStore.Models;

public partial class StaffShift
{
    public int ShiftId { get; set; }

    public int StaffId { get; set; }

    public int WeekOffset { get; set; }

    public string DayName { get; set; } = null!;

    public string ShiftType { get; set; } = null!;

    public virtual User Staff { get; set; } = null!;
}
