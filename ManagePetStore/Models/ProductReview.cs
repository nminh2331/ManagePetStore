using System;
using System.Collections.Generic;

namespace ManagePetStore.Models;

public partial class ProductReview
{
    public int ReviewId { get; set; }

    public string ProductSku { get; set; } = null!;

    public int CustomerId { get; set; }

    public int Rating { get; set; }

    public string? Comment { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual Customer Customer { get; set; } = null!;

    public virtual Product Product { get; set; } = null!;
}
