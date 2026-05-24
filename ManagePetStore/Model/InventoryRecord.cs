using System;
using System.Collections.Generic;

namespace ManagePetStore.Model;

public partial class InventoryRecord
{
    public int RecordId { get; set; }

    public string RecordType { get; set; } = null!;

    public int CreatedBy { get; set; }

    public int? ApprovedBy { get; set; }

    public string? ApprovalStatus { get; set; }

    public decimal? TotalAmount { get; set; }

    public string? Notes { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual User? ApprovedByNavigation { get; set; }

    public virtual User CreatedByNavigation { get; set; } = null!;

    public virtual ICollection<InternalConsumption> InternalConsumptions { get; set; } = new List<InternalConsumption>();

    public virtual ICollection<InventoryRecordDetail> InventoryRecordDetails { get; set; } = new List<InventoryRecordDetail>();
}
