using System;
using System.Collections.Generic;

namespace ManagePetStore.Model;

public partial class SpaService
{
    public int ServiceId { get; set; }

    public string ServiceName { get; set; } = null!;

    public decimal BasePrice { get; set; }

    public int? DurationMinutes { get; set; }

    public bool? IsActive { get; set; }

    public virtual ICollection<Booking> Bookings { get; set; } = new List<Booking>();
}
