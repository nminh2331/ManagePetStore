using System;
using System.Collections.Generic;

namespace ManagePetStore.Model;

public partial class HotelBooking
{
    public int HotelBookingId { get; set; }

    public int CustomerId { get; set; }

    public int PetId { get; set; }

    public int RoomId { get; set; }

    public DateOnly CheckInDate { get; set; }

    public DateOnly CheckOutDate { get; set; }

    public DateTime? ActualCheckIn { get; set; }

    public DateTime? ActualCheckOut { get; set; }

    public decimal? InitialWeight { get; set; }

    public string? InitialCoatStatus { get; set; }

    public string? InitialInjuries { get; set; }

    public string? InitialBehaviourNotes { get; set; }

    public decimal BaseDailyRate { get; set; }

    public decimal? TotalCost { get; set; }

    public string? Status { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual Customer Customer { get; set; } = null!;

    public virtual ICollection<DailyLog> DailyLogs { get; set; } = new List<DailyLog>();

    public virtual ICollection<InternalConsumption> InternalConsumptions { get; set; } = new List<InternalConsumption>();

    public virtual ICollection<OrderDetail> OrderDetails { get; set; } = new List<OrderDetail>();

    public virtual Pet Pet { get; set; } = null!;

    public virtual Room Room { get; set; } = null!;
}
