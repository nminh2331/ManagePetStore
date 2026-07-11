using System;
using System.Collections.Generic;

namespace ManagePetStore.Models;

public partial class Cage
{
    public string CageId { get; set; } = null!;

    public int RoomTypeId { get; set; }

    public string Status { get; set; } = null!;

    public string? ImageUrl { get; set; }

    public string? FeedSchedule { get; set; }

    public int Portion { get; set; }

    public virtual ICollection<FoodDiaryLog> FoodDiaryLogs { get; set; } = new List<FoodDiaryLog>();

    public virtual ICollection<HotelBooking> HotelBookings { get; set; } = new List<HotelBooking>();

    public virtual RoomMaintenanceLog? RoomMaintenanceLog { get; set; }

    public virtual RoomType RoomType { get; set; } = null!;
}
