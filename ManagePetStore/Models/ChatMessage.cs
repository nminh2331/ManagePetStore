using System;

namespace ManagePetStore.Models;

/// <summary>
/// Đại diện cho tin nhắn chat trong một ChatSession
/// </summary>
public partial class ChatMessage
{
    public int Id { get; set; }

    public int SessionId { get; set; }

    public int SenderId { get; set; }

    public string MessageText { get; set; } = null!;

    public DateTime SentAt { get; set; } = DateTime.Now;

    // Navigation properties
    public virtual ChatSession ChatSession { get; set; } = null!;

    public virtual User Sender { get; set; } = null!;
}
