using System;
using System.Collections.Generic;

namespace ManagePetStore.Model;

public partial class Pet
{
    public int PetId { get; set; }

    public int CustomerId { get; set; }

    public string PetName { get; set; } = null!;

    public string Species { get; set; } = null!;

    public string? Breed { get; set; }

    public decimal? Weight { get; set; }

    public DateOnly? DateOfBirth { get; set; }

    public string? MedicalNotes { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual ICollection<Booking> Bookings { get; set; } = new List<Booking>();

    public virtual Customer Customer { get; set; } = null!;

    public virtual ICollection<HotelBooking> HotelBookings { get; set; } = new List<HotelBooking>();

    public virtual ICollection<VaccinationSchedule> VaccinationSchedules { get; set; } = new List<VaccinationSchedule>();
}
