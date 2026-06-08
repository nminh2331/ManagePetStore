using System;
using System.Collections.Generic;

namespace ManagePetStore.Models;

public partial class Consumable
{
    public string ConsumableId { get; set; } = null!;

    public string Name { get; set; } = null!;

    public string Category { get; set; } = null!;

    public int Stock { get; set; }

    public int MinStock { get; set; }

    public string Unit { get; set; } = null!;
}
