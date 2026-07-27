using System;
using System.Collections.Generic;

namespace ManagePetStore.Models;

/// <summary>
/// NGƯỜI THỰC HIỆN: Nhật Minh
/// CHỨC NĂNG: Model đại diện cho Danh mục Dịch vụ Spa (SpaService).
/// </summary>
public partial class SpaService
{
    public int ServiceId { get; set; }

    public string Name { get; set; } = null!;

    public int DurationMinutes { get; set; }

    public decimal Price { get; set; }

    public bool Active { get; set; }

    public string? TargetSpecies { get; set; }

    public string? Description { get; set; }

    public string? ImageUrls { get; set; }

    public virtual ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
    public virtual ICollection<SpaBooking> SpaBookings { get; set; } = new List<SpaBooking>();
}
