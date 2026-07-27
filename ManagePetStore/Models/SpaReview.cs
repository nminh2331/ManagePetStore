using System;
using System.Collections.Generic;

using System.ComponentModel.DataAnnotations;

namespace ManagePetStore.Models;

/// <summary>
/// NGƯỜI THỰC HIỆN: Nhật Minh
/// CHỨC NĂNG: Model đại diện cho Đánh giá & Nhận xét Dịch vụ Spa (SpaReview).
/// </summary>
public partial class SpaReview
{
    [Key]
    public int ReviewId { get; set; }

    public int BookingId { get; set; }

    public int ServiceId { get; set; }

    public int GroomerId { get; set; }

    public int RatingStar { get; set; }

    public string? Comment { get; set; }

    public string? ImageUrl { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual SpaBooking Booking { get; set; } = null!;
}
