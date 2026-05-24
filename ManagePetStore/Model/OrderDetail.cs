using System;
using System.Collections.Generic;

namespace ManagePetStore.Model;

public partial class OrderDetail
{
    public int OrderDetailId { get; set; }

    public int OrderId { get; set; }

    public int? ProductId { get; set; }

    public int? BookingId { get; set; }

    public int? HotelBookingId { get; set; }

    public bool? IsCombo { get; set; }

    public string? ComboDescription { get; set; }

    public int Quantity { get; set; }

    public decimal UnitPrice { get; set; }

    public decimal SubTotal { get; set; }

    public virtual Booking? Booking { get; set; }

    public virtual HotelBooking? HotelBooking { get; set; }

    public virtual Order Order { get; set; } = null!;

    public virtual Product? Product { get; set; }
}
