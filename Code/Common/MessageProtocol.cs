using System.Buffers.Binary;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace ChatTCP.Common.Protocol;

public static class MessageProtocol
{
    // Giới hạn gói tin tối đa là 10MB để tránh tấn công từ chối dịch vụ (DoS)
    private const int MaxPacketSize = 10 * 1024 * 1024;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Đóng gói một gói tin và gửi qua NetworkStream.
    /// </summary>
    public static async Task SendPacketAsync<T>(NetworkStream networkStream, T packet, CancellationToken cancellationToken = default)
    {
        if (networkStream == null || !networkStream.CanWrite) 
            return;

        try
        {
            // Chuyển đổi gói tin thành chuỗi JSON và sau đó sang mảng byte
            string jsonString = JsonSerializer.Serialize(packet,JsonOptions);
            byte[] payloadBytes = Encoding.UTF8.GetBytes(jsonString);

            // Tạo header 4 byte chứa độ dài của payload
            byte[] fullFrame = new byte[4 + payloadBytes.Length];
            BinaryPrimitives.WriteInt32BigEndian(fullFrame.AsSpan(0,4), payloadBytes.Length);

            Buffer.BlockCopy(payloadBytes, 0, fullFrame, 4, payloadBytes.Length);

            // Gửi header và payload qua NetworkStream
            await networkStream.WriteAsync(fullFrame.AsMemory(), cancellationToken);
            await networkStream.FlushAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Lỗi gửi gói tin: {ex.Message}");
        }
    }

    /// <summary>
    /// Đọc một gói tin từ NetworkStream và trả về chuỗi JSON thô.
    /// </summary>
    public static async Task<string?> ReceiveRawJsonAsync(NetworkStream networkStream, CancellationToken cancellationToken = default)
    {
        if (networkStream == null || !networkStream.CanRead) 
            return null;

        try
        {
            // Đọc header 4 byte để xác định độ dài của payload
            byte[] lengthHeader = new byte[4];
            int headerRead = await ReadExactBytesAsync(networkStream, lengthHeader, 4, cancellationToken);
            if (headerRead == 0) 
                return null;
            if (headerRead < 4) 
                throw new EndOfStreamException("Gặp sự cố ngắt mạng giữa chừng khi đang đọc header.");
           
            // Chuyển đổi header từ big-endian sang int
            int payloadLength = BinaryPrimitives.ReadInt32BigEndian(lengthHeader);
            if (payloadLength <= 0 || payloadLength > MaxPacketSize)
                throw new InvalidDataException($"Độ dài gói tin không hợp lệ: {payloadLength} bytes.");

            // Đọc payload dựa trên độ dài đã xác định
            byte[] payloadBytes = new byte[payloadLength];
            int dataRead = await ReadExactBytesAsync(networkStream, payloadBytes, payloadLength, cancellationToken);
            if (dataRead < payloadLength)
                throw new EndOfStreamException("Gói tin bị cắt cụt (xé gói) do kết nối bị ngắt.");

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
    private static async Task<int> ReadExactBytesAsync(NetworkStream networkStream, byte[] buffer, int bytesToRead, CancellationToken cancellationToken)
    {
        int totalBytesRead = 0;
        while (totalBytesRead < bytesToRead)
        {
            int read = await networkStream.ReadAsync(buffer.AsMemory(totalBytesRead, bytesToRead - totalBytesRead), cancellationToken);
            if (read == 0) 
                break;
            totalBytesRead += read;
        }
        return totalBytesRead;
    }
}