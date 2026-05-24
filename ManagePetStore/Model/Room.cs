using System;
using System.Collections.Generic;

namespace ManagePetStore.Model;

public partial class Room
{
    public int RoomId { get; set; }

    public string RoomName { get; set; } = null!;

    public int RoomTypeId { get; set; }

    public string? Status { get; set; }

    public virtual ICollection<HotelBooking> HotelBookings { get; set; } = new List<HotelBooking>();

    public virtual RoomType RoomType { get; set; } = null!;
}
