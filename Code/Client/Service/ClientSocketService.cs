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
    public class ClientSocketService
    {
        private TcpClient? _client;

        private NetworkStream? _stream;

        private CancellationTokenSource?
            _cts;

        private readonly Dispatcher?
            _dispatcher;

        private readonly SemaphoreSlim
            _sendLock =
                new(
                    1,
                    1);

        private int _seq;

        // =====================================================
        // EVENTS
        // =====================================================

        public event Action<
            Packet<LoginResponseData>>?
            OnLoginResponse;

        public event Action<
            Packet<OnlineUsersData>>?
            OnOnlineUsersReceived;

        public event Action<
            Packet<ConversationListData>>?
            OnConversationListReceived;

        public event Action<
            Packet<ConversationHistoryData>>?
            OnConversationHistoryReceived;

        public event Action<
            Packet<UserStatusNotifyData>>?
            OnUserStatusChanged;

        public event Action<
            Packet<UserProfileNotifyData>>?
            OnUserProfileChanged;

        public event Action<
            Packet<AvatarUpdateResponseData>>?
            OnAvatarUpdated;

        public event Action<
            Packet<ChatMessageData>>?
            OnChatMessageReceived;

        public event Action<string>?
            OnError;

        public event Action?
            OnDisconnected;

        public ClientSocketService(
            Dispatcher? dispatcher = null)
        {
            _dispatcher =
                dispatcher;
        }

        public bool IsConnected =>
            _client != null
            &&
            _client.Connected
            &&
            _stream != null;

        // =====================================================
        // CONNECT
        // =====================================================

        public async Task<bool> ConnectAsync(
            string host = "127.0.0.1",
            int port = 8888)
        {
            if (IsConnected)
            {
                return true;
            }

            try
            {
                _client =
                    new TcpClient();

                await _client.ConnectAsync(
                    host,
                    port);

                _stream =
                    _client.GetStream();

                _cts =
                    new CancellationTokenSource();

                _ =
                    Task.Run(
                        () =>
                            ReceiveLoopAsync(
                                _cts.Token));

                return true;
            }
            catch (Exception ex)
            {
                RaiseError(
                    "Không thể kết nối server: "
                    + ex.Message);

                Disconnect();

                return false;
            }
        }

        // =====================================================
        // REQUESTS
        // =====================================================

        public Task LoginAsync(
            string username,
            string password)
        {
            return SendPacketAsync(
                new Packet<LoginRequestData>
                {
                    Type =
                        "LOGIN_REQ",

                    Seq =
                        NextSeq(),

                    Timestamp =
                        UnixNow(),

                    Data =
                        new LoginRequestData
                        {
                            Username =
                                username,

                            Password =
                                password
                        }
                });
        }

        public Task RequestOnlineUsersAsync()
        {
            return SendPacketAsync(
                new Packet<object>
                {
                    Type =
                        "ONLINE_USERS_REQ",

                    Seq =
                        NextSeq(),

                    Timestamp =
                        UnixNow(),

                    Data =
                        new { }
                });
        }

        public Task RequestConversationListAsync()
        {
            return SendPacketAsync(
                new Packet<object>
                {
                    Type =
                        "CONVERSATION_LIST_REQ",

                    Seq =
                        NextSeq(),

                    Timestamp =
                        UnixNow(),

                    Data =
                        new { }
                });
        }

        public Task RequestConversationHistoryAsync(
            int conversationId)
        {
            return SendPacketAsync(
                new Packet<ConversationHistoryRequestData>
                {
                    Type =
                        "CONVERSATION_HISTORY_REQ",

                    Seq =
                        NextSeq(),

                    Timestamp =
                        UnixNow(),

                    Data =
                        new ConversationHistoryRequestData
                        {
                            ConversationId =
                                conversationId
                        }
                });
        }

        public Task SendChatMessageAsync(
            ChatMessageData data)
        {
            return SendPacketAsync(
                new Packet<ChatMessageData>
                {
                    Type =
                        "CHAT_MSG",

                    Seq =
                        NextSeq(),

                    Timestamp =
                        UnixNow(),

                    Data =
                        data
                });
        }

        public Task UpdateAvatarAsync(
            string avatarData)
        {
            return SendPacketAsync(
                new Packet<AvatarUpdateRequestData>
                {
                    Type =
                        "AVATAR_UPDATE_REQ",

                    Seq =
                        NextSeq(),

                    Timestamp =
                        UnixNow(),

                    Data =
                        new AvatarUpdateRequestData
                        {
                            AvatarData =
                                avatarData
                        }
                });
        }

        // =====================================================
        // SEND
        // =====================================================

        private async Task SendPacketAsync<T>(
            Packet<T> packet)
        {
            if (!IsConnected
                ||
                _stream == null)
            {
                throw new InvalidOperationException(
                    "Client chưa kết nối server.");
            }

            await _sendLock.WaitAsync();

            try
            {
                await MessageProtocol
                    .SendPacketAsync(
                        _stream,
                        packet);
            }
            finally
            {
                _sendLock.Release();
            }
        }

        // =====================================================
        // RECEIVE
        // =====================================================

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

                    Packet<JsonElement>? packet;

                    try
                    {
                        packet =
                            JsonSerializer.Deserialize<
                                Packet<JsonElement>>(
                                raw);
                    }
                    catch
                    {
                        continue;
                    }

                    if (packet == null)
                    {
                        continue;
                    }

                    switch (packet.Type)
                    {
                        case "LOGIN_RES":

                            FirePacket<
                                LoginResponseData>(
                                raw,
                                OnLoginResponse);

                            break;

                        case "ONLINE_USERS_RES":

                            FirePacket<
                                OnlineUsersData>(
                                raw,
                                OnOnlineUsersReceived);

                            break;

                        case "CONVERSATION_LIST_RES":

                            FirePacket<
                                ConversationListData>(
                                raw,
                                OnConversationListReceived);

                            break;

                        case "CONVERSATION_HISTORY_RES":

                            FirePacket<
                                ConversationHistoryData>(
                                raw,
                                OnConversationHistoryReceived);

                            break;

                        case "USER_STATUS_NOTIFY":

                            FirePacket<
                                UserStatusNotifyData>(
                                raw,
                                OnUserStatusChanged);

                            break;

                        case "USER_PROFILE_NOTIFY":

                            FirePacket<
                                UserProfileNotifyData>(
                                raw,
                                OnUserProfileChanged);

                            break;

                        case "AVATAR_UPDATE_RES":

                            FirePacket<
                                AvatarUpdateResponseData>(
                                raw,
                                OnAvatarUpdated);

                            break;

                        case "CHAT_MSG":

                            FirePacket<
                                ChatMessageData>(
                                raw,
                                OnChatMessageReceived);

                            break;

                        case "ERROR":

                            HandleError(
                                raw);

                            break;
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                if (!token
                    .IsCancellationRequested)
                {
                    RaiseError(
                        ex.Message);
                }
            }
        }

        private void FirePacket<T>(
            string raw,
            Action<Packet<T>>? handler)
        {
            if (handler == null)
            {
                return;
            }

            Packet<T>? packet;

            try
            {
                packet =
                    JsonSerializer.Deserialize<
                        Packet<T>>(
                        raw);
            }
            catch
            {
                return;
            }

            if (packet == null)
            {
                return;
            }

            InvokeOnUI(
                () =>
                    handler.Invoke(
                        packet));
        }

        private void HandleError(
            string raw)
        {
            try
            {
                Packet<ErrorData>? packet =
                    JsonSerializer.Deserialize<
                        Packet<ErrorData>>(
                        raw);

                if (packet?.Data != null)
                {
                    RaiseError(
                        packet.Data.Message);
                }
            }
            catch
            {
            }
        }

        // =====================================================
        // DISCONNECT
        // =====================================================

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

            _stream = null;
            _client = null;
            _cts = null;

            InvokeOnUI(
                () =>
                    OnDisconnected?
                        .Invoke());
        }

        // =====================================================
        // HELPERS
        // =====================================================

        private int NextSeq()
        {
            return Interlocked
                .Increment(
                    ref _seq);
        }

        private static long UnixNow()
        {
            return DateTimeOffset.UtcNow
                .ToUnixTimeSeconds();
        }

        private void RaiseError(
            string message)
        {
            InvokeOnUI(
                () =>
                    OnError?
                        .Invoke(
                            message));
        }

        private void InvokeOnUI(
            Action action)
        {
            if (_dispatcher == null
                ||
                _dispatcher.CheckAccess())
            {
                action();
                return;
            }

            _dispatcher.BeginInvoke(
                action);
        }
    }
}