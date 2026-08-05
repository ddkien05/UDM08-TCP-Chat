using System;

namespace ChatTCP.Common.Payloads;

public class ReplyPayload
{
    public string MessageId { get; set; } = Guid.NewGuid().ToString();
    public string Content { get; set; } = string.Empty;          // Nội dung tin nhắn reply
    public string RepliedMessageId { get; set; } = string.Empty; // ID tin nhắn gốc
    public string RepliedSenderName { get; set; } = string.Empty;// Tên người gửi tin nhắn gốc
    public string RepliedContent { get; set; } = string.Empty;   // Nội dung trích dẫn
}