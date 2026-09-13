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
    /// Phát các sự kiện trên Dispatcher được cung cấp (nếu có)
    /// để người đăng ký có thể cập nhật UI an toàn.
    /// </summary>
    public class ClientSocketService
    {
        private TcpClient? _client;
        private NetworkStream? _stream;
        private CancellationTokenSource? _cts;
        private readonly Dispatcher? _dispatcher;

        public ClientSocketService(Dispatcher? dispatcher = null)
        {
            _dispatcher = dispatcher;
        }

        public bool IsConnected =>
            _client != null && _client.Connected && _stream != null;

        
        // EVENTS
        

        public event Action<Packet<ChatMessageData>>?
            OnChatMessageReceived;

        public event Action<Packet<UserStatusNotifyData>>?
            OnUserStatusChanged;

        public event Action<string>?
            OnError;

        public event Action?
            OnDisconnected;

        
        // CONNECT
        

        public async Task<bool> ConnectAsync(
            string host = "127.0.0.1",
            int port = 8888)
        {
            try
            {
                Disconnect();

                _client = new TcpClient();

                await _client.ConnectAsync(
                    host,
                    port);

                _stream = _client.GetStream();

                _cts = new CancellationTokenSource();

                _ = Task.Run(
                    () => ReceiveLoopAsync(_cts.Token));

                return true;
            }
            catch (Exception ex)
            {
                Disconnect();

                RaiseError(
                    $"Connect error: {ex.Message}");

                return false;
            }
        }

        
        // DISCONNECT
        

        public void Disconnect()
        {
            try
            {
                _cts?.Cancel();
            }
            catch
            {
            }

            try
            {
                _stream?.Close();
                _client?.Close();
            }
            catch
            {
            }
            finally
            {
                _stream = null;
                _client = null;
                _cts = null;

                InvokeOnUI(
                    () => OnDisconnected?.Invoke());
            }
        }

        
        // SEND PACKET
        

        public async Task SendPacketAsync<T>(
            Packet<T> packet)
        {
            if (!IsConnected || _stream == null)
            {
                throw new InvalidOperationException(
                    "Not connected");
            }

            try
            {
                await MessageProtocol.SendPacketAsync(
                    _stream,
                    packet);
            }
            catch (Exception ex)
            {
                RaiseError(
                    $"Send error: {ex.Message}");

                Disconnect();
            }
        }

        
        // SEND CHAT MESSAGE
        

        public async Task SendChatMessageAsync(
            ChatMessageData data)
        {
            var packet =
                new Packet<ChatMessageData>
                {
                    Type = "CHAT_MSG",

                    Seq = 0,

                    Timestamp =
                        DateTimeOffset.UtcNow
                            .ToUnixTimeSeconds(),

                    Data = data
                };

            await SendPacketAsync(packet);
        }

        
        // RECEIVE LOOP
        

        private async Task ReceiveLoopAsync(
            CancellationToken token)
        {
            if (_stream == null)
            {
                return;
            }

            try
            {
                while (!token.IsCancellationRequested)
                {
                    string? raw =
                        await MessageProtocol
                            .ReceiveRawJsonAsync(
                                _stream);

                    if (raw == null)
                    {
                        break;
                    }

                    Packet<JsonElement>? basePacket =
                        null;

                    try
                    {
                        basePacket =
                            JsonSerializer.Deserialize<
                                Packet<JsonElement>>(
                                raw);
                    }
                    catch
                    {
                        // Bỏ qua JSON lỗi
                    }

                    if (basePacket == null)
                    {
                        continue;
                    }

                    switch (basePacket.Type)
                    {
                        case "CHAT_MSG":

                            TryHandleChatMessage(raw);

                            break;

                        case "USER_STATUS_NOTIFY":

                            TryHandleUserStatus(raw);

                            break;

                        case "ERROR":

                            TryHandleError(raw);

                            break;

                            /*
                             * Có thể mở rộng sau:
                             *
                             * LOGIN_RES
                             * REGISTER_RES
                             * CONTACT_LIST_RES
                             * USER_SEARCH_RES
                             * AVATAR_RES
                             */
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Disconnect chủ động.
            }
            catch (Exception ex)
            {
                if (!token.IsCancellationRequested)
                {
                    RaiseError(
                        $"Receive loop error: {ex.Message}");
                }
            }
            finally
            {
                Disconnect();
            }
        }

        
        // CHAT MESSAGE
        

        private void TryHandleChatMessage(
            string raw)
        {
            try
            {
                var packet =
                    JsonSerializer.Deserialize<
                        Packet<ChatMessageData>>(
                        raw);

                if (packet != null)
                {
                    InvokeOnUI(
                        () =>
                            OnChatMessageReceived?
                                .Invoke(packet));
                }
            }
            catch (Exception ex)
            {
                RaiseError(
                    $"Receive CHAT_MSG parse error: {ex.Message}");
            }
        }

        
        // ONLINE / OFFLINE NOTIFY
        

        private void TryHandleUserStatus(
            string raw)
        {
            try
            {
                var packet =
                    JsonSerializer.Deserialize<
                        Packet<UserStatusNotifyData>>(
                        raw);

                if (packet != null)
                {
                    InvokeOnUI(
                        () =>
                            OnUserStatusChanged?
                                .Invoke(packet));
                }
            }
            catch (Exception ex)
            {
                RaiseError(
                    "Receive USER_STATUS_NOTIFY " +
                    $"parse error: {ex.Message}");
            }
        }

        
        // ERROR PACKET
        

        private void TryHandleError(
            string raw)
        {
            try
            {
                var packet =
                    JsonSerializer.Deserialize<
                        Packet<ErrorData>>(
                        raw);

                if (packet?.Data != null)
                {
                    RaiseError(
                        $"Server {packet.Data.Code}: " +
                        packet.Data.Message);
                }
            }
            catch (Exception ex)
            {
                RaiseError(
                    $"Receive ERROR parse error: {ex.Message}");
            }
        }

        
        // UI THREAD
        

        private void InvokeOnUI(
            Action action)
        {
            if (_dispatcher != null)
            {
                try
                {
                    if (_dispatcher.CheckAccess())
                    {
                        action();
                    }
                    else
                    {
                        _dispatcher.BeginInvoke(
                            action);
                    }
                }
                catch
                {
                    // Không để Dispatcher làm crash app.
                }
            }
            else
            {
                try
                {
                    Task.Run(action);
                }
                catch
                {
                }
            }
        }

        
        // ERROR EVENT
        

        private void RaiseError(
            string message)
        {
            InvokeOnUI(
                () => OnError?.Invoke(message));
        }
    }
}