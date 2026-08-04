using System;

namespace ChatTCP.Common.Models;

public class Packet
{
    public string PacketId { get; set; } = Guid.NewGuid().ToString();
    public MessageType Type { get; set; }
    public string SenderId { get; set; } = string.Empty;
    public string ReceiverId { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.Now;

    /// <summary>
    /// Chuỗi JSON chứa dữ liệu chi tiết (Payload) tương ứng với từng MessageType
    /// </summary>
    public string PayloadJson { get; set; } = string.Empty;
}