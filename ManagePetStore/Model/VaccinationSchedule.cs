using System;
using System.Collections.Generic;

namespace ManagePetStore.Model;

public partial class VaccinationSchedule
{
    public int ScheduleId { get; set; }

    public int PetId { get; set; }

    public string VaccineName { get; set; } = null!;

    public DateOnly InjectionDate { get; set; }

    public DateOnly NextDueDate { get; set; }

    public string? Status { get; set; }

    public string? Notes { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual Pet Pet { get; set; } = null!;
}
