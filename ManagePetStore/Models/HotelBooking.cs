using System;
using System.Collections.Generic;

namespace ManagePetStore.Models;

public partial class HotelBooking
{
    public int HotelBookingId { get; set; }

    public string CageId { get; set; } = null!;

    public int PetId { get; set; }

    public int CustomerId { get; set; }

    public DateTime CheckInDate { get; set; }

    public DateTime? CheckOutDate { get; set; }

    public int StayDays { get; set; }

    public decimal BaseDailyPrice { get; set; }

    public decimal Subtotal { get; set; }

    public decimal Discount { get; set; }

    public decimal FinalAmount { get; set; }

    public int EarnedPoints { get; set; }

    public string Status { get; set; } = null!;

    public virtual ICollection<BookingAddon> BookingAddons { get; set; } = new List<BookingAddon>();

    public virtual Cage Cage { get; set; } = null!;

    public virtual Customer Customer { get; set; } = null!;

    public virtual Pet Pet { get; set; } = null!;
}
