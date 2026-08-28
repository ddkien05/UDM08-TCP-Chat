using System;
using System.Net.Sockets;
using System.Text;
using Common;

class Program
{
    static TcpClient client = new();

    static StreamReader? reader;
    static StreamWriter? writer;

    // Tin nhắn đang được Reply
    static int? replyToId = null;

    static string? replyToContent = null;

    static async Task Main(string[] args)
    {
        Console.Write("Nhập tên của bạn: ");
        string? username = Console.ReadLine();

        if (string.IsNullOrWhiteSpace(username))
            return;

        Console.Write("Nhập IP Server: ");
        string? ip = Console.ReadLine();

        if (string.IsNullOrWhiteSpace(ip))
            ip = "127.0.0.1";

        try
        {
            await client.ConnectAsync(ip, 5000);

            NetworkStream stream = client.GetStream();

            reader = new StreamReader(
                stream,
                Encoding.UTF8
            );

            writer = new StreamWriter(
                stream,
                Encoding.UTF8
            )
            {
                AutoFlush = true
            };

            // Gửi username cho server
            await writer.WriteLineAsync(username);

            Console.WriteLine();
            Console.WriteLine("Đã kết nối server!");
            Console.WriteLine();
            Console.WriteLine("=================================");
            Console.WriteLine(" LỆNH");
            Console.WriteLine(" /reply <ID>  → Reply tin nhắn");
            Console.WriteLine(" /cancel      → Hủy Reply");
            Console.WriteLine(" /exit        → Thoát");
            Console.WriteLine("=================================");
            Console.WriteLine();

            // Nhận tin nhắn từ server
            _ = ReceiveLoop();

            // Gửi tin nhắn
            await SendLoop();
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                $"Không thể kết nối server: {ex.Message}"
            );
        }
    }

    static async Task SendLoop()
    {
        while (true)
        {
            Console.Write("Bạn: ");

            string? input = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(input))
                continue;

            // Thoát
            if (input.Equals(
                "/exit",
                StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            // Hủy Reply
            if (input.Equals(
                "/cancel",
                StringComparison.OrdinalIgnoreCase))
            {
                replyToId = null;
                replyToContent = null;

                Console.WriteLine(
                    "Đã hủy Reply."
                );

                continue;
            }

            // Chọn Reply
            if (input.StartsWith(
                "/reply ",
                StringComparison.OrdinalIgnoreCase))
            {
                string idText = input.Substring(7);

                if (int.TryParse(
                    idText,
                    out int id))
                {
                    replyToId = id;

                    Console.WriteLine(
                        $"Đang Reply tin nhắn ID = {id}"
                    );

                    Console.Write(
                        "Nội dung Reply: "
                    );

                    string? content =
                        Console.ReadLine();

                    if (string.IsNullOrWhiteSpace(content))
                    {
                        replyToId = null;
                        continue;
                    }

                    Console.Write(
                        "Gửi đến: "
                    );

                    string? receiver =
                        Console.ReadLine();

                    if (string.IsNullOrWhiteSpace(receiver))
                    {
                        replyToId = null;
                        continue;
                    }

                    Message message = new Message
                    {
                        Sender = "",
                        Receiver = receiver,
                        Content = content,
                        ReplyToId = replyToId,
                        ReplyToContent = "Tin nhắn ID " + id,
                        SentAt = DateTime.Now
                    };

                    await SendMessage(message);

                    replyToId = null;
                    replyToContent = null;
                }
                else
                {
                    Console.WriteLine(
                        "ID không hợp lệ."
                    );
                }

                continue;
            }

            // Tin nhắn bình thường
            Console.Write(
                "Gửi đến: "
            );

            string? receiverName =
                Console.ReadLine();

            if (string.IsNullOrWhiteSpace(receiverName))
                continue;

            Message normalMessage = new Message
            {
                Sender = "",
                Receiver = receiverName,
                Content = input,
                ReplyToId = null,
                ReplyToContent = null,
                SentAt = DateTime.Now
            };

            await SendMessage(normalMessage);
        }
    }

    static async Task SendMessage(Message message)
    {
        if (writer == null)
            return;

        string json = message.ToJson();

        await writer.WriteLineAsync(json);
    }

    static async Task ReceiveLoop()
    {
        if (reader == null)
            return;

        try
        {
            while (true)
            {
                string? line =
                    await reader.ReadLineAsync();

                if (line == null)
                    break;

                if (line.StartsWith("SERVER:"))
                {
                    Console.WriteLine();
                    Console.WriteLine(line);
                    continue;
                }

                Message? message =
                    Message.FromJson(line);

                if (message == null)
                    continue;

                Console.WriteLine();

                if (message.ReplyToId != null)
                {
                    Console.WriteLine(
                        $"┌── ↩ Reply tin nhắn " +
                        $"ID {message.ReplyToId}"
                    );

                    Console.WriteLine(
                        $"│ Tin nhắn gốc: " +
                        $"{message.ReplyToContent}"
                    );

                    Console.WriteLine(
                        $"│ {message.Sender}: " +
                        $"{message.Content}"
                    );

                    Console.WriteLine("└──────────────");
                }
                else
                {
                    Console.WriteLine(
                        $"[{message.MessageId}] " +
                        $"{message.Sender}: " +
                        $"{message.Content}"
                    );
                }

                Console.WriteLine();
            }
        }
        catch
        {
            Console.WriteLine(
                "Mất kết nối với server."
            );
        }
    }
}
