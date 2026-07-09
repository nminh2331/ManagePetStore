using System;
using System.Collections.Generic;

namespace ManagePetStore.Models;

public partial class RoomMaintenanceLog
{
    public int MaintenanceLogId { get; set; }

    public string CageId { get; set; } = null!;

    public string? PreviousStatus { get; set; }

    public string NewStatus { get; set; } = null!;

    public string Reason { get; set; } = null!;

    public DateTime StartedAt { get; set; }

    public DateTime? EndedAt { get; set; }

    public int? CreatedByUserId { get; set; }

    public int? EndedByUserId { get; set; }

    public string? CreatedByName { get; set; }

    public string? EndedByName { get; set; }

    public string? Note { get; set; }

    public virtual Cage Cage { get; set; } = null!;

    public virtual User? CreatedByUser { get; set; }

    public virtual User? EndedByUser { get; set; }
}
