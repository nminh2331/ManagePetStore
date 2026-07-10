using System;
using System.Collections.Generic;

using System.ComponentModel.DataAnnotations;

namespace ManagePetStore.Models;

public partial class SpaReview
{
    [Key]
    public int ReviewId { get; set; }

    public int BookingId { get; set; }

    public int ServiceId { get; set; }

    public int GroomerId { get; set; }

    public int RatingStar { get; set; }

    public string? Comment { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual SpaBooking Booking { get; set; } = null!;
}
