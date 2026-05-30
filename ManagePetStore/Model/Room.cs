using System;
using System.Collections.Generic;

namespace ManagePetStore.Model;

public partial class Room
{
    public int RoomId { get; set; }

    public string RoomCode { get; set; } = null!;

    public string RoomType { get; set; } = null!;

    public decimal DailyRate { get; set; }

    public string? Status { get; set; }

    public string? Dimensions { get; set; }

    public virtual ICollection<HotelBooking> HotelBookings { get; set; } = new List<HotelBooking>();
}
