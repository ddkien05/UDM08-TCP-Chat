using System.Text.Json.Serialization;

namespace ChatTCP.Common.Models;

/// <summary>
/// Khung gói tin dùng để truyền dữ liệu giữa client và server.
/// </summary>
public class Packet<T>
{
    [JsonPropertyName("type")] public string Type { get; set; } = string.Empty;
    [JsonPropertyName("seq")] public int Seq { get; set; }
    [JsonPropertyName("timestamp")] public long Timestamp { get; set; }
    [JsonPropertyName("data")] public T Data { get; set; } = default!;
}

/// <summary>
/// Dữ liệu gửi từ client đến server để xác thực người dùng.
/// </summary>
public class AuthRequestData
{
    [JsonPropertyName("username")] public string Username { get; set; } = string.Empty;
    [JsonPropertyName("display_name")] public string DisplayName { get; set; } = string.Empty;
    [JsonPropertyName("avatar_url")] public string? AvatarUrl { get; set; }
}

/// <summary>
/// Dữ liệu trả về từ server sau khi xác thực thành công.
/// </summary>
public class AuthResponseData
{
    [JsonPropertyName("code")] public int Code { get; set; }
    [JsonPropertyName("message")] public string Message { get; set; } = string.Empty;
    [JsonPropertyName("user_id")] public string UserId { get; set; } = string.Empty;
    [JsonPropertyName("display_name")] public string DisplayName { get; set; } = string.Empty;
    [JsonPropertyName("avatar_url")] public string? AvatarUrl { get; set; }
}

/// <summary>
/// Thông tin người gửi trong một tin nhắn.
/// </summary>
public class SenderInfo
{
    [JsonPropertyName("user_id")] public string UserId { get; set; } = string.Empty;
    [JsonPropertyName("display_name")] public string DisplayName { get; set; } = string.Empty;
    [JsonPropertyName("avatar_url")] public string? AvatarUrl { get; set; }
}

/// <summary>
/// Thông tin về tin nhắn mà người dùng đang trả lời.
/// </summary>
public class ReplyInfo
{
    [JsonPropertyName("msg_id")] public string MsgId { get; set; } = string.Empty;
    [JsonPropertyName("sender_name")] public string SenderName { get; set; } = string.Empty;
    [JsonPropertyName("content_snippet")] public string ContentSnippet { get; set; } = string.Empty;
}

/// <summary>
/// Dữ liệu của một tin nhắn chat.
/// </summary>
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

/// <summary>
/// Dữ liệu thông báo trạng thái người dùng (online/offline) từ server.
/// </summary>
public class UserStatusNotifyData
{
    [JsonPropertyName("user_id")] public string UserId { get; set; } = string.Empty;
    [JsonPropertyName("display_name")] public string DisplayName { get; set; } = string.Empty;
    [JsonPropertyName("status")] public string Status { get; set; } = "ONLINE"; // "ONLINE" hoặc "OFFLINE"
    [JsonPropertyName("last_seen")] public long LastSeen { get; set; }
}

/// <summary>
/// Dữ liệu lỗi trả về từ server.
/// </summary>
public class ErrorData
{
    [JsonPropertyName("code")] public int Code { get; set; }
    [JsonPropertyName("message")] public string Message { get; set; } = string.Empty;
}