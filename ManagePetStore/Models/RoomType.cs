using System;
using System.Collections.Generic;

namespace ManagePetStore.Models;

public partial class RoomType
{
    public int RoomTypeId { get; set; }

    public string Code { get; set; } = null!;

    public string Type { get; set; } = null!;

    public string Size { get; set; } = null!;

    public int Capacity { get; set; }

    public decimal HourlyPrice { get; set; }

    public decimal DailyPrice { get; set; }

    public bool HasAc { get; set; }

    public bool HasCamera { get; set; }

    public bool HasPremiumFood { get; set; }

    public bool Status { get; set; }

    public virtual ICollection<Cage> Cages { get; set; } = new List<Cage>();

    public virtual ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();

    public virtual ICollection<HotelCageStaySegment> HotelCageStaySegments { get; set; } = new List<HotelCageStaySegment>();
}
