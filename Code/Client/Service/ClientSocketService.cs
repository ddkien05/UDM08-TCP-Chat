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
    /// Trợ giúp socket phía client cho ứng dụng có giao diện (UI).
    /// Xử lý kết nối/đóng kết nối, gửi và vòng lặp nhận dữ liệu chạy nền.
    /// Phát các sự kiện trên Dispatcher được cung cấp (nếu có) để người đăng ký có thể cập nhật UI an toàn.
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

        // Sự kiện
        public event Action<Packet<ChatMessageData>>? OnChatMessageReceived;
        public event Action<string>? OnError;
        public event Action? OnDisconnected;

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
                // Xử lý như đã mất kết nối
                Disconnect();
            }
        }

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

                    if (basePacket.Type == "CHAT_MSG")
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
                    else
                    {
                        // Có thể mở rộng để phát sự kiện chung cho các loại packet khác.
                        // Hiện tại bỏ qua.
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
                // Không có Dispatcher, thực thi trên threadpool
                try { Task.Run(action); } catch { }
            }
        }

        private void RaiseError(string message)
        {
            InvokeOnUI(() => OnError?.Invoke(message));
        }
    }
}
