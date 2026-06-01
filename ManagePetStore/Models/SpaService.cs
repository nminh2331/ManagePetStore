using System;
using System.Collections.Generic;

namespace ManagePetStore.Models;

public partial class SpaService
{
    public int ServiceId { get; set; }

    public string Name { get; set; } = null!;

    public int DurationMinutes { get; set; }

    public decimal Price { get; set; }

    public bool Active { get; set; }

    public virtual ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();

    public virtual ICollection<SpaBooking> SpaBookings { get; set; } = new List<SpaBooking>();
}
