using System;
using System.Collections.Generic;

namespace ManagePetStore.Model;

public partial class InternalConsumption
{
    public int ConsumptionId { get; set; }

    public int? RecordId { get; set; }

    public int? HotelBookingId { get; set; }

    public int? BookingId { get; set; }

    public int ProductId { get; set; }

    public int Quantity { get; set; }

    public DateTime? ConsumedAt { get; set; }

    public virtual Booking? Booking { get; set; }

    public virtual HotelBooking? HotelBooking { get; set; }

    public virtual Product Product { get; set; } = null!;

    public virtual InventoryRecord? Record { get; set; }
}
