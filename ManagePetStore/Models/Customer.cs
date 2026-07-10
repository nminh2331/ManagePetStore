using System;
using System.Collections.Generic;

namespace ManagePetStore.Models;

public partial class Customer
{
    public int CustomerId { get; set; }

    public int? UserId { get; set; }

    public string FullName { get; set; } = null!;

    public string Phone { get; set; } = null!;

    public string? Email { get; set; }

    public int LoyaltyPoints { get; set; }

    public string MembershipTier { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public virtual ICollection<HotelBooking> HotelBookings { get; set; } = new List<HotelBooking>();

    public virtual ICollection<Order> Orders { get; set; } = new List<Order>();

    public virtual ICollection<Pet> Pets { get; set; } = new List<Pet>();

    public virtual ICollection<ReturnRequest> ReturnRequests { get; set; } = new List<ReturnRequest>();

    public virtual ICollection<SpaBooking> SpaBookings { get; set; } = new List<SpaBooking>();

    public virtual ICollection<StaffTask> StaffTasks { get; set; } = new List<StaffTask>();

    public virtual User? User { get; set; }

    public virtual ICollection<Wallet> Wallets { get; set; } = new List<Wallet>();
}
