using System;
using System.Collections.Generic;

namespace ManagePetStore.Models;

public partial class PetBioTimeline
{
    public int TimelineId { get; set; }

    public int PetId { get; set; }

    public DateTime Date { get; set; }

    public string Title { get; set; } = null!;

    public string Description { get; set; } = null!;

    public string Type { get; set; } = null!;

    public virtual Pet Pet { get; set; } = null!;
}
