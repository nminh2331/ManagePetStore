using System;
using System.Collections.Generic;

namespace ManagePetStore.Models;

public partial class Voucher
{
    public string Code { get; set; } = null!;

    public string Type { get; set; } = null!;

    public decimal Value { get; set; }

    public decimal MinOrder { get; set; }

    public DateTime ExpiryDate { get; set; }

    public bool Status { get; set; }
}
