using System;
using System.Collections.Generic;

namespace ManagePetStore.Models;

/// <summary>
/// Đại diện cho phiên chat giữa Customer và Manager
/// </summary>
public partial class ChatSession
{
    public int Id { get; set; }

    public int CustomerId { get; set; }

    public int? ManagerId { get; set; }

    /// <summary>
    /// Trạng thái phiên chat: "Waiting", "Active", "Closed"
    /// </summary>
    public string Status { get; set; } = "Waiting";

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public DateTime LastMessageAt { get; set; } = DateTime.Now;

    // Navigation properties
    public virtual User Customer { get; set; } = null!;

    public virtual User? Manager { get; set; }

    public virtual ICollection<ChatMessage> ChatMessages { get; set; } = new List<ChatMessage>();
}
