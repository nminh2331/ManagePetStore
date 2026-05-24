using System;
using System.Collections.Generic;

namespace ManagePetStore.Model;

public partial class DailyLog
{
    public int LogId { get; set; }

    public int HotelBookingId { get; set; }

    public DateTime? LogDate { get; set; }

    public string EatingStatus { get; set; } = null!;

    public string PottyStatus { get; set; } = null!;

    public string? StaffNotes { get; set; }

    public string? MediaUrl { get; set; }

    public int StaffId { get; set; }

    public virtual HotelBooking HotelBooking { get; set; } = null!;

    public virtual User Staff { get; set; } = null!;
}
