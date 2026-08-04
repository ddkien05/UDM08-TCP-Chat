namespace ChatTCP.Common.Models;

public enum MessageType
{
    // --- 1. Hệ thống & Đăng nhập ---
    LoginRequest,
    LoginResponse,
    Logout,
    UserListUpdate,

    // --- 2. Chức năng Chat chính ---
    TextMessage,      // Tin nhắn văn bản + Emoji (UTF-8)
    ReplyMessage,    // Trả lời tin nhắn
    ForwardMessage,  // Chuyển tiếp tin nhắn

    // --- 3. Profile & Avatar ---
    UpdateAvatarRequest,
    UpdateAvatarResponse,

    // --- 4. Thông báo lỗi ---
    Error
}