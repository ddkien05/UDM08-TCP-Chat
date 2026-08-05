using System;
using System.Text;
using System.Text.Json;
using ChatTCP.Common.Models;

namespace ChatTCP.Common.Helpers;

public static class PacketHelper
{
 
    /// Đóng gói Packet thành byte array có 4 bytes header chỉ độ dài (tránh dính gói TCP)

    public static byte[] Serialize(Packet packet)
    {
        string json = JsonSerializer.Serialize(packet);
        byte[] bodyBytes = Encoding.UTF8.GetBytes(json);
        byte[] lengthBytes = BitConverter.GetBytes(bodyBytes.Length);

        byte[] buffer = new byte[4 + bodyBytes.Length];
        Array.Copy(lengthBytes, 0, buffer, 0, 4);
        Array.Copy(bodyBytes, 0, buffer, 4, bodyBytes.Length);

        return buffer;
    }

    /// Tạo chuỗi PayloadJson từ Object dữ liệu
    public static string CreatePayload<T>(T payloadData)
    {
        return JsonSerializer.Serialize(payloadData);
    }

    /// Bóc tách chuỗi PayloadJson trong Packet ra Object cụ thể

    public static T? GetPayload<T>(Packet packet)
    {
        if (string.IsNullOrEmpty(packet.PayloadJson)) return default;
        return JsonSerializer.Deserialize<T>(packet.PayloadJson);
    }
}