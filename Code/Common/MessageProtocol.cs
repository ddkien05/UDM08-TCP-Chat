using System.Buffers.Binary;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace Common;

public static class MessageProtocol
{
    public static async Task SendPacketAsync<T>(NetworkStream ns, T p)
    {
        if (ns == null || !ns.CanWrite) return;

        byte[] buf = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(p));
        byte[] h = new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(h, buf.Length);

        await ns.WriteAsync(h.AsMemory(0, 4));
        await ns.WriteAsync(buf.AsMemory(0, buf.Length));
        await ns.FlushAsync();
    }

    public static async Task<string?> ReceiveRawJsonAsync(NetworkStream ns)
    {
        if (ns == null || !ns.CanRead) return null;

        byte[] h = new byte[4];
        if (await ReadAsync(ns, h, 4) < 4) return null;

        int len = BinaryPrimitives.ReadInt32BigEndian(h);
        if (len <= 0 || len > 10 * 1024 * 1024) return null;

        byte[] buf = new byte[len];
        if (await ReadAsync(ns, buf, len) < len) return null;

        return Encoding.UTF8.GetString(buf);
    }

    private static async Task<int> ReadAsync(NetworkStream ns, byte[] buf, int count)
    {
        int sum = 0;
        while (sum < count)
        {
            int r = await ns.ReadAsync(buf, sum, count - sum);
            if (r == 0) break;
            sum += r;
        }
        return sum;
    }
}