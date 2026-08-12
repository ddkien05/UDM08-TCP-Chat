using System;
using System.Text.Json;

namespace Common;

public class Message
{
    public int MessageId { get; set; }

    public string Sender { get; set; } = "";

    public string Receiver { get; set; } = "";

    public string Content { get; set; } = "";

    public DateTime SentAt { get; set; }

    // ID của tin nhắn đang được Reply
    public int? ReplyToId { get; set; }

    // Nội dung tin nhắn đang được Reply
    public string? ReplyToContent { get; set; }
    public bool IsForwarded { get; set; }
    public string? OriginalSender { get; set; }

    public string ToJson()
    {
        return JsonSerializer.Serialize(this);
    }

    public static Message? FromJson(string json)
    {
        return JsonSerializer.Deserialize<Message>(json);
    }
}