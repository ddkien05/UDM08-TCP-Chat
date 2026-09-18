//<<<<<<< HEAD
//﻿//using System.Collections.Concurrent;
////using System.Net;
////using System.Net.Sockets;
////using System.Text;
////using System.Text.Json;
////using ChatTCP.Common.Models;
////using ChatTCP.Common.Protocol;
////using ChatTCP.Server.Services;
//=======
//﻿using System.Collections.Concurrent;
//using System.Net;
//using System.Net.Sockets;
//using System.Text;
//using System.Text.Json;
//using ChatTCP.Common.Models;
//using ChatTCP.Common.Protocol;
//using ChatTCP.Server.Services;
//using ChatTCP.Server.Data;
//>>>>>>> 4dcb71763c935529c19ee7b2e4889d4a0bcc8679

////namespace ChatTCP.Server.Networking;

////class MessageTestServer
////{
////    private static readonly ConcurrentDictionary<string, NetworkStream> _clients = new();

////    static async Task Main()
////    {
////        Console.OutputEncoding = Encoding.UTF8;
////        int port = 8888;

//<<<<<<< HEAD
////        var router = new MessageRouter(_clients);
////        var listener = new TcpListener(IPAddress.Any, port);
////        listener.Start();
//=======
//        IMessageRepository messageRepository = new MessageRepository();
//        var router = new MessageRouter(_clients, messageRepository);
//        var listener = new TcpListener(IPAddress.Any, port);
//        listener.Start();
//>>>>>>> 4dcb71763c935529c19ee7b2e4889d4a0bcc8679

////        Console.Title = "TCP SERVER";
////        Console.WriteLine($"[SERVER] Server is running on port {port}...");

////        while (true)
////        {
////            try
////            {
////                var client = await listener.AcceptTcpClientAsync();
////                _ = Task.Run(() => HandleClientAsync(client, router));
////            }
////            catch (Exception ex)
////            {
////                Console.WriteLine($"[ERROR] Error accepting client: {ex.Message}");
////            }
////        }
////    }

////    private static async Task HandleClientAsync(TcpClient tcpClient, MessageRouter router)
////    {
////        var networkStream = tcpClient.GetStream();
////        string? userId = null;

////        try
////        {
////            while (true)
////            {
////                string? raw = await MessageProtocol.ReceiveRawJsonAsync(networkStream);
////                if (raw == null) break;

////                var basePacket = JsonSerializer.Deserialize<Packet<JsonElement>>(raw);
////                if (basePacket == null) continue;

////                // Đăng nhập
////                if (basePacket.Type == "AUTH_REQ")
////                {
////                    userId = basePacket.Data.GetProperty("username").GetString() ?? "unknown";
////                    _clients[userId] = networkStream;
////                    Console.WriteLine($"[+] User {userId} Online");

////                    var authRes = new Packet<object>
////                    {
////                        Type = "AUTH_RES",
////                        Seq = basePacket.Seq,
////                        Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
////                        Data = new { code = 200, message = "Success", user_id = userId }
////                    };
////                    await MessageProtocol.SendPacketAsync(networkStream, authRes);
////                }
////                // Chat 1-1
////                else if (basePacket.Type == "CHAT_MSG")
////                {
////                    var chatPacket = JsonSerializer.Deserialize<Packet<ChatMessageData>>(raw);
////                    if (chatPacket != null) await router.RouteChatMessageAsync(chatPacket, networkStream);
////                }
////            }
////        }
////        catch (Exception ex) 
////        {
////            Console.WriteLine($"[ERROR] Error handling client: {ex.Message}");
////        }
////        finally
////        {
////            if (!string.IsNullOrEmpty(userId))
////            {
////                _clients.TryRemove(userId, out _);
////                Console.WriteLine($"[-] User {userId} Offline");
////            }
////            networkStream.Close();
////            tcpClient.Close();
////        }
////    }
////}