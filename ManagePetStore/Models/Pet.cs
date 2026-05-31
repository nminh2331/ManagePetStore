using System;
using System.Collections.Generic;

namespace ManagePetStore.Models;

public partial class Pet
{
    public int PetId { get; set; }

    public int CustomerId { get; set; }

    public string Name { get; set; } = null!;

    public string Species { get; set; } = null!;

    public string? Breed { get; set; }

    public decimal Weight { get; set; }

    public string? Age { get; set; }

    public string? Status { get; set; }

    public string? Pathology { get; set; }

    public string? ImageUrl { get; set; }

    public virtual Customer Customer { get; set; } = null!;

    public virtual ICollection<HotelBooking> HotelBookings { get; set; } = new List<HotelBooking>();

    public virtual ICollection<PetBioTimeline> PetBioTimelines { get; set; } = new List<PetBioTimeline>();

    public virtual ICollection<SpaBooking> SpaBookings { get; set; } = new List<SpaBooking>();

    public virtual ICollection<StaffTask> StaffTasks { get; set; } = new List<StaffTask>();
}
