using System;
using System.Collections.Generic;

namespace ManagePetStore.Models;

public partial class ChatMessage
{
    public int Id { get; set; }

    public int SessionId { get; set; }

    public int SenderId { get; set; }

    public string MessageText { get; set; } = null!;

    public DateTime SentAt { get; set; }

    public virtual User Sender { get; set; } = null!;

    public virtual ChatSession Session { get; set; } = null!;
}
