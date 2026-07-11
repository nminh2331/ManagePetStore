using System;
using System.Collections.Generic;

namespace ManagePetStore.Models;

public partial class MedicalRecord
{
    public int RecordId { get; set; }

    public int PetId { get; set; }

    public int? HotelBookingId { get; set; }

    public DateTime DateCreated { get; set; }

    public decimal Weight { get; set; }

    public string HealthStatus { get; set; } = null!;

    public string? Symptoms { get; set; }

    public string? Treatment { get; set; }

    public string? VaccinationStatus { get; set; }

    public string? ParasitePrevention { get; set; }

    public string? PhysicalCheck { get; set; }

    public string? ShellStatus { get; set; }

    public string? RearingConditions { get; set; }

    public string? AbnormalSymptoms { get; set; }

    public string? IncisorCheck { get; set; }

    public string? FurSkinCheck { get; set; }

    public string? DigestiveSigns { get; set; }

    public virtual HotelBooking? HotelBooking { get; set; }

    public virtual Pet Pet { get; set; } = null!;
}
