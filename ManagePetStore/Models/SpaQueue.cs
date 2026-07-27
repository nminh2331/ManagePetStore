using System;
using System.Collections.Generic;

namespace ManagePetStore.Models;

/// <summary>
/// NGƯỜI THỰC HIỆN: Nhật Minh
/// CHỨC NĂNG: Model đại diện cho Hàng đợi Real-time & Tiếp nhận Khách vãng lai Spa (SpaQueue).
/// </summary>
public partial class SpaQueue
{
    public int QueueId { get; set; }

    public string QueueNumber { get; set; } = null!;

    public string PetName { get; set; } = null!;

    public string OwnerName { get; set; } = null!;

    public DateTime ArrivalTime { get; set; }

    public string? ServiceDescription { get; set; }

    public string? Note { get; set; }
}
