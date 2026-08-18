using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using Common;

class Program
{
    static TcpClient client = new TcpClient();

    static StreamReader? reader;
    static StreamWriter? writer;

    // Lưu các tin nhắn đã nhận
    static List<Message> receivedMessages = new List<Message>();

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

            // Gửi tên người dùng cho Server
            await writer.WriteLineAsync(username);

            Console.WriteLine();
            Console.WriteLine("Đã kết nối Server!");
            Console.WriteLine();

            Console.WriteLine("=================================");
            Console.WriteLine(" CÁC LỆNH");
            Console.WriteLine(" /reply <ID>   → Trả lời tin nhắn");
            Console.WriteLine(" /forward <ID> → Chuyển tiếp tin nhắn");
            Console.WriteLine(" /exit         → Thoát");
            Console.WriteLine("=================================");
            Console.WriteLine();

            // Nhận tin nhắn từ Server
            _ = ReceiveLoop();

            // Gửi tin nhắn
            await SendLoop();
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                $"Không thể kết nối Server: {ex.Message}"
            );
        }
    }


    // ==========================================
    // GỬI TIN NHẮN
    // ==========================================

    static async Task SendLoop()
    {
        while (true)
        {
            Console.Write("Bạn: ");

            string? input = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(input))
                continue;


            // ==================================
            // THOÁT
            // ==================================

            if (input.Equals(
                "/exit",
                StringComparison.OrdinalIgnoreCase))
            {
                break;
            }


            // ==================================
            // REPLY
            // ==================================

            if (input.StartsWith(
                "/reply ",
                StringComparison.OrdinalIgnoreCase))
            {
                string idText = input.Substring(7);

                if (!int.TryParse(idText, out int id))
                {
                    Console.WriteLine(
                        "ID không hợp lệ."
                    );

                    continue;
                }

                Message? originalMessage =
                    receivedMessages.Find(
                        m => m.MessageId == id
                    );

                if (originalMessage == null)
                {
                    Console.WriteLine(
                        $"Không tìm thấy tin nhắn ID {id}."
                    );

                    continue;
                }

                Console.WriteLine();
                Console.WriteLine("====== REPLY ======");
                Console.WriteLine(
                    $"Người gửi: {originalMessage.Sender}"
                );

                Console.WriteLine(
                    $"Tin nhắn: {originalMessage.Content}"
                );

                Console.WriteLine("===================");

                Console.Write("Nội dung Reply: ");

                string? replyContent =
                    Console.ReadLine();

                if (string.IsNullOrWhiteSpace(replyContent))
                    continue;

                Console.Write("Gửi đến: ");

                string? receiver =
                    Console.ReadLine();

                if (string.IsNullOrWhiteSpace(receiver))
                    continue;

                Message replyMessage = new Message
                {
                    Sender = "",
                    Receiver = receiver,
                    Content = replyContent,
                    ReplyToId = originalMessage.MessageId,
                    ReplyToContent = originalMessage.Content,
                    SentAt = DateTime.Now
                };

                await SendMessage(replyMessage);

                continue;
            }


            // ==================================
            // FORWARD
            // ==================================

            if (input.StartsWith(
                "/forward ",
                StringComparison.OrdinalIgnoreCase))
            {
                string idText = input.Substring(9);

                if (!int.TryParse(idText, out int id))
                {
                    Console.WriteLine(
                        "ID không hợp lệ."
                    );

                    continue;
                }

                Message? originalMessage =
                    receivedMessages.Find(
                        m => m.MessageId == id
                    );

                if (originalMessage == null)
                {
                    Console.WriteLine(
                        $"Không tìm thấy tin nhắn ID {id}."
                    );

                    continue;
                }

                Console.WriteLine();
                Console.WriteLine("====== FORWARD ======");

                Console.WriteLine(
                    $"Người gửi gốc: {originalMessage.Sender}"
                );

                Console.WriteLine(
                    $"Nội dung: {originalMessage.Content}"
                );

                Console.WriteLine("=====================");

                Console.Write("Gửi đến: ");

                string? receiver =
                    Console.ReadLine();

                if (string.IsNullOrWhiteSpace(receiver))
                    continue;

                Message forwardMessage = new Message
                {
                    Sender = "",
                    Receiver = receiver,
                    Content = originalMessage.Content,
                    SentAt = DateTime.Now,

                    IsForwarded = true,

                    OriginalSender =
                        originalMessage.Sender
                };

                await SendMessage(forwardMessage);

                Console.WriteLine(
                    $"Đã Forward tin nhắn ID {id}."
                );

                continue;
            }


            // ==================================
            // TIN NHẮN BÌNH THƯỜNG
            // ==================================

            Console.Write("Gửi đến: ");

            string? receiverName =
                Console.ReadLine();

            if (string.IsNullOrWhiteSpace(receiverName))
                continue;

            Message normalMessage = new Message
            {
                Sender = "",
                Receiver = receiverName,
                Content = input,
                SentAt = DateTime.Now,

                ReplyToId = null,
                ReplyToContent = null,

                IsForwarded = false,
                OriginalSender = null
            };

            await SendMessage(normalMessage);
        }
    }


    // ==========================================
    // GỬI MESSAGE LÊN SERVER
    // ==========================================

    static async Task SendMessage(Message message)
    {
        if (writer == null)
            return;

        string json = message.ToJson();

        await writer.WriteLineAsync(json);
    }


    // ==========================================
    // NHẬN MESSAGE TỪ SERVER
    // ==========================================

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


                // Lưu tin nhắn để có thể Reply / Forward
                receivedMessages.Add(message);


                Console.WriteLine();


                // ==================================
                // NẾU LÀ FORWARD
                // ==================================

                if (message.IsForwarded)
                {
                    Console.WriteLine(
                        "┌── ↪ FORWARD"
                    );

                    Console.WriteLine(
                        $"│ Từ: {message.OriginalSender}"
                    );

                    Console.WriteLine(
                        $"│ {message.Content}"
                    );

                    Console.WriteLine(
                        "└──────────────"
                    );
                }


                // ==================================
                // NẾU LÀ REPLY
                // ==================================

                else if (message.ReplyToId != null)
                {
                    Console.WriteLine(
                        $"┌── ↩ REPLY " +
                        $"tin nhắn ID {message.ReplyToId}"
                    );

                    Console.WriteLine(
                        $"│ Tin nhắn gốc: " +
                        $"{message.ReplyToContent}"
                    );

                    Console.WriteLine(
                        $"│ {message.Sender}: " +
                        $"{message.Content}"
                    );

                    Console.WriteLine(
                        "└──────────────"
                    );
                }


                // ==================================
                // TIN NHẮN BÌNH THƯỜNG
                // ==================================

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
                "Mất kết nối với Server."
            );
        }
    }
}