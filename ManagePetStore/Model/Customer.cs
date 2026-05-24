using System;
using System.Collections.Generic;

namespace ManagePetStore.Model;

public partial class Customer
{
    public int CustomerId { get; set; }

    public string FullName { get; set; } = null!;

    public string Phone { get; set; } = null!;

    public string? Email { get; set; }

    public string? PasswordHash { get; set; }

    public int? LoyaltyPoints { get; set; }

    public string? MembershipTier { get; set; }

    public string? Status { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual ICollection<Booking> Bookings { get; set; } = new List<Booking>();

    public virtual ICollection<Cart> Carts { get; set; } = new List<Cart>();

    public virtual ICollection<HotelBooking> HotelBookings { get; set; } = new List<HotelBooking>();

    public virtual ICollection<Order> Orders { get; set; } = new List<Order>();

    public virtual ICollection<Pet> Pets { get; set; } = new List<Pet>();
}
