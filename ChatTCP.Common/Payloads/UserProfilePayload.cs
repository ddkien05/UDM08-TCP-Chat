namespace ChatTCP.Common.Payloads;

public class UserProfilePayload
{
    public string UserId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string AvatarBase64 { get; set; } = string.Empty; // Chuỗi ảnh Base64
    public bool IsOnline { get; set; }
}