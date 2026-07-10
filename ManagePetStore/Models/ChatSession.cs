using System;
using System.Collections.Generic;

namespace ManagePetStore.Models;

public partial class ChatSession
{
    public int Id { get; set; }

    public int CustomerId { get; set; }

    public int? ManagerId { get; set; }

    public string Status { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime LastMessageAt { get; set; }

    public virtual ICollection<ChatMessage> ChatMessages { get; set; } = new List<ChatMessage>();

    public virtual User Customer { get; set; } = null!;

    public virtual User? Manager { get; set; }
}
