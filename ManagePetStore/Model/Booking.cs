using System;
using System.Collections.Generic;

namespace ManagePetStore.Model;

public partial class Booking
{
    public int BookingId { get; set; }

    public int CustomerId { get; set; }

    public int PetId { get; set; }

    public int ServiceId { get; set; }

    public int? AssignedStaffId { get; set; }

    public DateOnly BookingDate { get; set; }

    public TimeOnly BookingTime { get; set; }

    public string? Status { get; set; }

    public decimal PriceCharged { get; set; }

    public string? Notes { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual User? AssignedStaff { get; set; }

    public virtual Customer Customer { get; set; } = null!;

    public virtual ICollection<InternalConsumption> InternalConsumptions { get; set; } = new List<InternalConsumption>();

    public virtual ICollection<OrderDetail> OrderDetails { get; set; } = new List<OrderDetail>();

    public virtual Pet Pet { get; set; } = null!;

    public virtual SpaService Service { get; set; } = null!;
}
