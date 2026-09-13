using System;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using ChatTCP.Common.Models;
using ChatTCP.Common.Protocol;

namespace ChatTCP.Client.Networking
{
    /// <summary>
    /// ClientSocketService manages TCP socket communication for the WPF chat client.
    /// 
    /// Responsibilities:
    /// - Establish/disconnect TCP connections to the chat server
    /// - Send and receive packets (AUTH_REQ, CHAT_MSG, BROADCAST, ERROR, etc.)
    /// - Run a background receive loop to handle incoming messages
    /// - Dispatch events on the UI thread for safe collection updates
    /// 
    /// Key Features:
    /// - Thread-safe event dispatching via WPF Dispatcher
    /// - Async/await pattern for non-blocking I/O
    /// - Automatic reconnection on send failures
    /// - Support for multiple message types: regular, reply, forward, broadcast
    /// 
    /// Usage:
    ///   var service = new ClientSocketService(Dispatcher.CurrentDispatcher);
    ///   await service.ConnectAsync("127.0.0.1", 9000);
    ///   service.OnChatMessageReceived += HandleMessage;
    ///   await service.SendChatMessageAsync(data);
    /// </summary>
    public class ClientSocketService
    {
        private TcpClient? _client;
        private NetworkStream? _stream;
        private CancellationTokenSource? _cts;
        private readonly Dispatcher? _dispatcher;

        public ClientSocketService(Dispatcher? dispatcher = null) {
            _dispatcher = dispatcher;
        }

        public bool IsConnected =>
            _client != null && _client.Connected && _stream != null;

        /// <summary>
        /// Fired when a regular CHAT_MSG packet is received from server.
        /// Includes Reply, Forward, and standard message types.
        /// </summary>
        public event Action<Packet<ChatMessageData>>? OnChatMessageReceived;

        /// <summary>
        /// Fired when a BROADCAST packet is received (message from server to all clients).
        /// </summary>
        public event Action<Packet<ChatMessageData>>? OnBroadcastReceived;

        /// <summary>
        /// Fired when an ERROR packet is received or a connection error occurs.
        /// </summary>
        public event Action<string>? OnError;

        /// <summary>
        /// Fired when the connection is closed or lost.
        /// </summary>
        public event Action? OnDisconnected;

        /// <summary>
        /// Establishes a TCP connection to the chat server and starts the receive loop.
        /// 
        /// Protocol Flow:
        /// 1. Connect via TCP to host:port
        /// 2. Start background receive loop that parses incoming packets
        /// 3. Server typically responds with AUTH_RSP after initial connection
        /// 
        /// Error Handling:
        /// - Connection failures invoke OnError and return false
        /// - Receive loop exits automatically on stream errors
        /// </summary>
        /// <param name="host">Server IP address (default: localhost)</param>
        /// <param name="port">Server TCP port (default: 9000)</param>
        /// <returns>True if connection successful, false if failed</returns>
        public async Task<bool> ConnectAsync(string host = "127.0.0.1", int port = 9000)
        {
            try
            {
                Disconnect();

                _client = new TcpClient();
                await _client.ConnectAsync(host, port);
                _stream = _client.GetStream();

                _cts = new CancellationTokenSource();
                _ = Task.Run(() => ReceiveLoopAsync(_cts.Token));

                return true;
            }
            catch (Exception ex)
            {
                Disconnect();
                RaiseError($"Connect error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Closes the TCP connection and cancels the receive loop.
        /// Safe to call multiple times.
        /// </summary>
        public void Disconnect()
        {
            try
            {
                _cts?.Cancel();
            }
            catch { }

            try
            {
                _stream?.Close();
                _client?.Close();
            }
            catch { }
            finally
            {
                _stream = null;
                _client = null;
                _cts = null;
                InvokeOnUI(() => OnDisconnected?.Invoke());
            }
        }

        /// <summary>
        /// Sends a generic packet to the server.
        /// Used internally by SendChatMessageAsync and other type-specific methods.
        /// 
        /// Protocol:
        /// - Packet is serialized to JSON
        /// - Message length header is prepended (via MessageProtocol)
        /// - Data is sent over TCP stream
        /// 
        /// Error Handling:
        /// - Exceptions trigger Disconnect() to clean up state
        /// - OnError event is raised with error details
        /// </summary>
        /// <typeparam name="T">Type of packet data (e.g., ChatMessageData, ErrorData)</typeparam>
        /// <param name="packet">Packet object to send</param>
        /// <returns>Task that completes when packet is written to stream</returns>
        public async Task SendPacketAsync<T>(Packet<T> packet)
        {
            if (!IsConnected || _stream == null) throw new InvalidOperationException("Not connected");

            try
            {
                await MessageProtocol.SendPacketAsync(_stream, packet);
            }
            catch (Exception ex)
            {
                RaiseError($"Send error: {ex.Message}");
                // Handle as disconnection
                Disconnect();
            }
        }

        /// <summary>
        /// High-level convenience method to send a chat message.
        /// Wraps ChatMessageData in a Packet with type "CHAT_MSG" and sends via socket.
        /// 
        /// Message Types Supported:
        /// - PRIVATE: Single recipient (data.TargetId = recipient UserId)
        /// - BROADCAST: All connected users (data.TargetId = "*")
        /// - GROUP: Group message (data.TargetId = group ID)
        /// 
        /// Special Handling:
        /// - If message has ReplyTo set, server processes as reply to original message
        /// - If IsForwarded = true, server marks as forwarded message
        /// - Timestamp is auto-set to current server time
        /// 
        /// Example:
        ///   var msg = new ChatMessageData { 
        ///       Content = "Hello!",
        ///       TargetType = "PRIVATE", 
        ///       TargetId = "user_102",
        ///       Sender = new SenderInfo { ... }
        ///   };
        ///   await service.SendChatMessageAsync(msg);
        /// </summary>
        /// <param name="data">Chat message data to send</param>
        /// <returns>Task that completes when message is sent to server</returns>
        public async Task SendChatMessageAsync(ChatMessageData data)
        {
            var packet = new Packet<ChatMessageData>
            {
                Type = "CHAT_MSG",
                Seq = 0,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Data = data
            };

            await SendPacketAsync(packet);
        }

        /// <summary>
        /// Background task that continuously receives and processes packets from server.
        /// Runs on a separate thread to avoid blocking the UI thread.
        /// 
        /// Packet Routes:
        /// - "CHAT_MSG" → OnChatMessageReceived (includes replies, forwards, broadcasts)
        /// - "BROADCAST" → OnBroadcastReceived (if separate, otherwise same as CHAT_MSG)
        /// - "ERROR" → OnError event with error message
        /// - Others → logged and ignored (extensible for future types)
        /// 
        /// Error Handling:
        /// - Malformed JSON is logged but doesn't crash the loop
        /// - Stream errors (disconnect) break the loop and trigger Disconnect()
        /// - Ensures graceful shutdown even with partial/corrupted data
        /// 
        /// Threading:
        /// - Runs on ThreadPool thread
        /// - Uses Dispatcher to invoke callbacks on UI thread
        /// - Observable to cancellation token for clean shutdown
        /// </summary>
        /// <param name="token">Cancellation token to stop the receive loop</param>
        private async Task ReceiveLoopAsync(CancellationToken token)
        {
            if (_stream == null) return;

            try
            {
                while (!token.IsCancellationRequested)
                {
                    string? raw = await MessageProtocol.ReceiveRawJsonAsync(_stream);
                    if (raw == null) break;

                    Packet<JsonElement>? basePacket = null;
                    try
                    {
                        basePacket = JsonSerializer.Deserialize<Packet<JsonElement>>(raw);
                    }
                    catch (Exception) { /* ignore malformed */ }

                    if (basePacket == null) continue;

                    // Route packet based on type
                    switch (basePacket.Type)
                    {
                        case "CHAT_MSG":
                            HandleChatMessage(raw);
                            break;

                        case "BROADCAST":
                            HandleBroadcast(raw);
                            break;

                        case "ERROR":
                            HandleError(raw);
                            break;

                        case "AUTH_RSP":
                            // Authentication response - could fire separate event
                            Console.WriteLine("Auth response received");
                            break;

                        default:
                            // Future packet types can be added here
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                RaiseError($"Receive loop error: {ex.Message}");
            }
            finally
            {
                Disconnect();
            }
        }

        /// <summary>
        /// Routes a CHAT_MSG packet to the OnChatMessageReceived event.
        /// Parses the raw JSON and validates before invoking.
        /// </summary>
        private void HandleChatMessage(string raw)
        {
            try
            {
                var chatPacket = JsonSerializer.Deserialize<Packet<ChatMessageData>>(raw);
                if (chatPacket != null)
                {
                    InvokeOnUI(() => OnChatMessageReceived?.Invoke(chatPacket));
                }
            }
            catch (Exception ex)
            {
                RaiseError($"Receive parse error: {ex.Message}");
            }
        }

        /// <summary>
        /// Routes a BROADCAST packet to the OnBroadcastReceived event.
        /// Similar to HandleChatMessage but for broadcast messages.
        /// </summary>
        private void HandleBroadcast(string raw)
        {
            try
            {
                var broadcastPacket = JsonSerializer.Deserialize<Packet<ChatMessageData>>(raw);
                if (broadcastPacket != null)
                {
                    InvokeOnUI(() => OnBroadcastReceived?.Invoke(broadcastPacket));
                }
            }
            catch (Exception ex)
            {
                RaiseError($"Broadcast parse error: {ex.Message}");
            }
        }

        /// <summary>
        /// Routes an ERROR packet to the OnError event.
        /// Extracts error code and message for display to user.
        /// </summary>
        private void HandleError(string raw)
        {
            try
            {
                var errorPacket = JsonSerializer.Deserialize<Packet<ErrorData>>(raw);
                if (errorPacket?.Data != null)
                {
                    RaiseError($"Server error {errorPacket.Data.Code}: {errorPacket.Data.Message}");
                }
            }
            catch (Exception ex)
            {
                RaiseError($"Error parse error: {ex.Message}");
            }
        }

        /// <summary>
        /// Invokes an action on the UI thread if Dispatcher is available.
        /// Otherwise executes on thread pool.
        /// 
        /// This ensures thread-safe updates to UI collections and controls.
        /// Called by all event-raising methods to guarantee UI thread execution.
        /// </summary>
        /// <param name="action">Callback to invoke on UI thread</param>
        private void InvokeOnUI(Action action)
        {
            if (_dispatcher != null)
            {
                try
                {
                    if (_dispatcher.CheckAccess())
                        action();
                    else
                        _dispatcher.BeginInvoke(action);
                }
                catch { /* swallow to avoid UI crash */ }
            }
            else
            {
                // No Dispatcher, execute on threadpool
                try { Task.Run(action); } catch { }
            }
        }

        /// <summary>
        /// Raises the OnError event with the given message.
        /// Always executed on UI thread via InvokeOnUI.
        /// </summary>
        /// <param name="message">Error message to report</param>
        private void RaiseError(string message)
        {
            InvokeOnUI(() => OnError?.Invoke(message));
        }
    }
}
