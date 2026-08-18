using System.Text.Json.Serialization;

namespace Common;

public class Packet<T>
{
    [JsonPropertyName("type")] public string Type { get; set; } = string.Empty;
    [JsonPropertyName("seq")] public int Seq { get; set; }
    [JsonPropertyName("timestamp")] public long Timestamp { get; set; }
    [JsonPropertyName("data")] public T Data { get; set; } = default!;
}

public class SenderInfo
{
    [JsonPropertyName("user_id")] public string UserId { get; set; } = string.Empty;
    [JsonPropertyName("display_name")] public string DisplayName { get; set; } = string.Empty;
    [JsonPropertyName("avatar_url")] public string? AvatarUrl { get; set; }
}

public class ReplyInfo
{
    [JsonPropertyName("msg_id")] public string MsgId { get; set; } = string.Empty;
    [JsonPropertyName("sender_name")] public string SenderName { get; set; } = string.Empty;
    [JsonPropertyName("content_snippet")] public string ContentSnippet { get; set; } = string.Empty;
}

public class ChatMessageData
{
    [JsonPropertyName("msg_id")] public string MsgId { get; set; } = string.Empty;
    [JsonPropertyName("target_type")] public string TargetType { get; set; } = "PRIVATE";
    [JsonPropertyName("target_id")] public string TargetId { get; set; } = string.Empty;
    [JsonPropertyName("sender")] public SenderInfo Sender { get; set; } = new();
    [JsonPropertyName("content")] public string Content { get; set; } = string.Empty;
    [JsonPropertyName("reply_to")] public ReplyInfo? ReplyTo { get; set; }
    [JsonPropertyName("is_forwarded")] public bool IsForwarded { get; set; }
    [JsonPropertyName("forward_from_name")] public string? ForwardFromName { get; set; }
}

public class ErrorData
{
    [JsonPropertyName("code")] public int Code { get; set; }
    [JsonPropertyName("message")] public string Message { get; set; } = string.Empty;
}