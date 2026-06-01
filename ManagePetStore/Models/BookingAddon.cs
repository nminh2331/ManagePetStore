using System;
using System.Collections.Generic;

namespace ManagePetStore.Models;

public partial class BookingAddon
{
    public int AddonId { get; set; }

    public int HotelBookingId { get; set; }

    public string Name { get; set; } = null!;

    public decimal Price { get; set; }

    public virtual HotelBooking HotelBooking { get; set; } = null!;
}
