using System.Buffers.Binary;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace ChatTCP.Common.Protocol;

public static class MessageProtocol
{
    /// <summary>
    /// Đóng gói một gói tin và gửi qua NetworkStream.
    /// </summary>
    public static async Task SendPacketAsync<T>(NetworkStream networkStream, T packet)
    {
        if (networkStream == null || !networkStream.CanWrite) return;

        try
        {
            // Chuyển đổi gói tin thành chuỗi JSON và sau đó sang mảng byte
            string jsonString = JsonSerializer.Serialize(packet);
            byte[] payloadBytes = Encoding.UTF8.GetBytes(jsonString);

            // Tạo header 4 byte chứa độ dài của payload
            byte[] lengthHeader = new byte[4];
            BinaryPrimitives.WriteInt32BigEndian(lengthHeader, payloadBytes.Length);

            // Gửi header và payload qua NetworkStream
            await networkStream.WriteAsync(lengthHeader.AsMemory(0, 4));
            await networkStream.WriteAsync(payloadBytes.AsMemory(0, payloadBytes.Length));
            await networkStream.FlushAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Lỗi gửi gói tin: {ex.Message}");
        }
    }

    /// <summary>
    /// Đọc một gói tin từ NetworkStream và trả về chuỗi JSON thô.
    /// </summary>
    public static async Task<string?> ReceiveRawJsonAsync(NetworkStream networkStream)
    {
        if (networkStream == null || !networkStream.CanRead) return null;

        try
        {
            // Đọc header 4 byte để xác định độ dài của payload
            byte[] lengthHeader = new byte[4];
            int headerBytesRead = await ReadExactBytesAsync(networkStream, lengthHeader, 4);
            if (headerBytesRead < 4) return null;

            // Chuyển đổi header từ big-endian sang int
            int payloadLength = BinaryPrimitives.ReadInt32BigEndian(lengthHeader);
            if (payloadLength <= 0 || payloadLength > 10 * 1024 * 1024) return null;

            // Đọc payload dựa trên độ dài đã xác định
            byte[] payloadBytes = new byte[payloadLength];
            int dataBytesRead = await ReadExactBytesAsync(networkStream, payloadBytes, payloadLength);
            if (dataBytesRead < payloadLength) return null;

            // Chuyển đổi payload từ mảng byte sang chuỗi JSON
            return Encoding.UTF8.GetString(payloadBytes);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Lỗi nhận gói tin: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Đọc chính xác số byte từ NetworkStream.
    /// </summary>
    private static async Task<int> ReadExactBytesAsync(NetworkStream networkStream, byte[] buffer, int bytesToRead)
    {
        int totalBytesRead = 0;
        while (totalBytesRead < bytesToRead)
        {
            int bytesRead = await networkStream.ReadAsync(buffer, totalBytesRead, bytesToRead - totalBytesRead);
            if (bytesRead == 0) break;
            totalBytesRead += bytesRead;
        }
        return totalBytesRead;
    }
}