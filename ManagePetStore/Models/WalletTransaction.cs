using System;
using System.Collections.Generic;

namespace ManagePetStore.Models;

public partial class WalletTransaction
{
    public int TransactionId { get; set; }

    public int WalletId { get; set; }

    public decimal Amount { get; set; }

    public string Type { get; set; } = null!;

    public string? Description { get; set; }

    public string? OrderId { get; set; }

    public DateTime TransactionDate { get; set; }

    public virtual Order? Order { get; set; }

    public virtual Wallet Wallet { get; set; } = null!;
}
