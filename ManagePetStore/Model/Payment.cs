using System;
using System.Collections.Generic;

namespace ManagePetStore.Model;

public partial class Payment
{
    public int PaymentId { get; set; }

    public int OrderId { get; set; }

    public string PaymentMethod { get; set; } = null!;

    public string? TransactionNo { get; set; }

    public decimal Amount { get; set; }

    public DateTime? PaymentDate { get; set; }

    public string? Status { get; set; }

    public string? Notes { get; set; }

    public virtual Order Order { get; set; } = null!;
}
