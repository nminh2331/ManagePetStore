using System;
using System.Collections.Generic;

namespace ManagePetStore.Models;

public partial class SpaBooking
{
    public int BookingId { get; set; }

    public int PetId { get; set; }

    public int CustomerId { get; set; }

    public int ServiceId { get; set; }

    public DateTime DateTime { get; set; }

    public int GroomerId { get; set; }

    public decimal Price { get; set; }

    public string Status { get; set; } = null!;

    public string SpaStatus { get; set; } = null!;

    public string? Notes { get; set; }

    public virtual Customer Customer { get; set; } = null!;

    public virtual User Groomer { get; set; } = null!;

    public virtual Pet Pet { get; set; } = null!;

    public virtual SpaService Service { get; set; } = null!;
}
