using System;
using System.Collections.Generic;

namespace ManagePetStore.Models;

public partial class HotelBooking
{
    public int HotelBookingId { get; set; }

    public string CageId { get; set; } = null!;

    public int PetId { get; set; }

    public int CustomerId { get; set; }

    public DateTime CheckInDate { get; set; }

    public DateTime? CheckOutDate { get; set; }

    public DateTime? ScheduledCheckInDate { get; set; }

    public DateTime? ScheduledCheckOutDate { get; set; }

    public DateTime? ActualCheckInAt { get; set; }

    public DateTime? ActualCheckOutAt { get; set; }

    public int StayDays { get; set; }

    public decimal BaseDailyPrice { get; set; }

    public decimal Subtotal { get; set; }

    public decimal Discount { get; set; }

    public decimal FinalAmount { get; set; }

    public int EarnedPoints { get; set; }

    public string Status { get; set; } = null!;

    public virtual ICollection<BookingAddon> BookingAddons { get; set; } = new List<BookingAddon>();

    public virtual ICollection<FoodDiaryLog> FoodDiaryLogs { get; set; } = new List<FoodDiaryLog>();

    public virtual ICollection<MedicalRecord> MedicalRecords { get; set; } = new List<MedicalRecord>();

    public virtual ICollection<PetBioTimeline> PetBioTimelines { get; set; } = new List<PetBioTimeline>();

    public virtual ICollection<HotelCageChangeRequest> CageChangeRequests { get; set; } = new List<HotelCageChangeRequest>();

    public virtual ICollection<HotelCageStaySegment> CageStaySegments { get; set; } = new List<HotelCageStaySegment>();

    public virtual Cage Cage { get; set; } = null!;

    public virtual Customer Customer { get; set; } = null!;

    public virtual Pet Pet { get; set; } = null!;
}
