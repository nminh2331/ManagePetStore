using System;
using System.Collections.Generic;

namespace ManagePetStore.Models;

public partial class Banner
{
    public int BannerId { get; set; }

    public string? Title { get; set; }

    public string ImageUrl { get; set; } = null!;

    public string? TargetUrl { get; set; }

    public int DisplayOrder { get; set; }

    public bool IsActive { get; set; }
}
