using System;
using System.Collections.Generic;

namespace ManagePetStore.Models;

public partial class StaffProfile
{
    public int UserId { get; set; }

    public int PerformanceScore { get; set; }

    public int TasksCompletedCount { get; set; }

    public decimal RatingsAverage { get; set; }

    public virtual User User { get; set; } = null!;
}
