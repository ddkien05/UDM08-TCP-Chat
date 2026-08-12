using TcpChat.Common.Enums;

namespace TcpChat.Common.Models;

public class ChatMessage
{
    public MessageType Type { get; set; }

    public string Sender { get; set; } = string.Empty;

    public string Receiver { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;

    public DateTime Timestamp { get; set; } = DateTime.Now;
}