using System;
using System.Collections.Generic;

namespace ManagePetStore.Models;

public partial class FoodDiaryLog
{
    public string LogId { get; set; } = null!;

    public string PetName { get; set; } = null!;

    public string CageId { get; set; } = null!;

    public int? HotelBookingId { get; set; }

    public string Status { get; set; } = null!;

    public string FoodType { get; set; } = null!;

    public string Amount { get; set; } = null!;

    public string? PhotoUrl { get; set; }

    public string? Note { get; set; }

    public string Time { get; set; } = null!;

    public DateTime? OccurredAt { get; set; }

    public string StaffName { get; set; } = null!;

    public virtual Cage Cage { get; set; } = null!;

    public virtual HotelBooking? HotelBooking { get; set; }
}
