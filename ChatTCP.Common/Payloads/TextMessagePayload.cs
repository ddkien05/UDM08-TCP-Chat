using System;

namespace ChatTCP.Common.Payloads;

public class TextMessagePayload
{
    public string MessageId { get; set; } = Guid.NewGuid().ToString();
    public string Content { get; set; } = string.Empty; // Nội dung bao gồm cả Emoji UTF-8
}