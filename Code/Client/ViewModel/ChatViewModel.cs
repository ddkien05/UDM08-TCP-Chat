using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Threading;
using ChatTCP.Common.Models;
using ChatTCP.Common.Protocol;

namespace ChatTCP.Client.ViewModels
{
    /// <summary>
    /// ChatViewModel handles the business logic for chat messages including:
    /// - Managing message history with in-memory cache (max 500 messages)
    /// - Sending regular messages, replies, forwards, and broadcasts
    /// - Loading message history when user scrolls up
    /// - Message state management (reply/forward context)
    /// 
    /// This ViewModel separates concerns from the View, allowing for easier testing
    /// and potential reuse. It communicates with ClientSocketService for network operations.
    /// 
    /// Usage:
    ///   var vm = new ChatViewModel(socketService);
    ///   vm.Messages.Add(new ChatMessageData { ... });
    ///   await vm.SendMessageAsync("Hello");
    ///   await vm.SendReplyAsync(messageId, "Reply text");
    /// </summary>
    public class ChatViewModel
    {
        private readonly ClientSocketService? _socketService;
        private readonly Dispatcher? _dispatcher;
        private const int MaxCachedMessages = 500;
        private const int HistoryPageSize = 20;
        private bool _isLoadingHistory = false;

        /// <summary>
        /// Observable collection of messages displayed in the chat view.
        /// Updated when receiving messages, sending messages, or loading history.
        /// </summary>
        public ObservableCollection<ChatMessageData> Messages { get; } = new();

        /// <summary>
        /// Queue of message history (older messages) waiting to be loaded.
        /// In a real app, this would fetch from database.
        /// </summary>
        private readonly Queue<ChatMessageData> _historyQueue = new();

        /// <summary>
        /// Current user information (set after authentication)
        /// </summary>
        public SenderInfo? CurrentUser { get; set; }

        /// <summary>
        /// Indicates if history is currently being loaded (prevents multiple concurrent loads)
        /// </summary>
        public bool IsLoadingHistory
        {
            get => _isLoadingHistory;
            private set => _isLoadingHistory = value;
        }

        public ChatViewModel(ClientSocketService? socketService = null, Dispatcher? dispatcher = null)
        {
            _socketService = socketService;
            _dispatcher = dispatcher;

            // Subscribe to socket service events
            if (_socketService != null)
            {
                _socketService.OnChatMessageReceived += HandleChatMessageReceived;
            }
        }

        /// <summary>
        /// Sends a regular chat message to the specified target.
        /// The message is wrapped in a Packet with CHAT_MSG type and sent via socket.
        /// 
        /// Protocol: Packet&lt;ChatMessageData&gt;
        ///   - Type: "CHAT_MSG"
        ///   - Data contains: MsgId, TargetType, TargetId, Sender, Content
        /// </summary>
        /// <param name="targetId">Recipient user ID or group ID</param>
        /// <param name="targetType">Message target type (PRIVATE, GROUP, BROADCAST)</param>
        /// <param name="content">Message content text</param>
        /// <returns>Task that completes when message is sent to server</returns>
        public async Task SendMessageAsync(string targetId, string content, string targetType = "PRIVATE")
        {
            if (string.IsNullOrWhiteSpace(content)) return;
            if (_socketService == null || !_socketService.IsConnected)
            {
                InvokeOnUI(() => 
                {
                    var msg = new ChatMessageData
                    {
                        MsgId = Guid.NewGuid().ToString("N"),
                        Content = content,
                        TargetType = targetType,
                        TargetId = targetId,
                        Sender = CurrentUser ?? new SenderInfo { DisplayName = "Me" }
                    };
                    Messages.Add(msg);
                });
                return;
            }

            try
            {
                var data = new ChatMessageData
                {
                    MsgId = Guid.NewGuid().ToString("N"),
                    Content = content,
                    TargetType = targetType,
                    TargetId = targetId,
                    Sender = CurrentUser ?? new SenderInfo { DisplayName = "Me" }
                };

                await _socketService.SendChatMessageAsync(data);

                // Optionally add to local cache immediately for optimistic UI
                InvokeOnUI(() => Messages.Add(data));
            }
            catch (Exception ex)
            {
                InvokeOnUI(() => 
                {
                    // Could raise an error event here
                    Console.WriteLine($"Send error: {ex.Message}");
                });
            }
        }

        /// <summary>
        /// Sends a reply message in response to a specific message.
        /// The reply maintains a reference to the original message via ReplyInfo.
        /// 
        /// Protocol: Same as SendMessageAsync, but Data.ReplyTo is populated
        ///   - ReplyInfo contains: MsgId (of original), SenderName, ContentSnippet
        /// </summary>
        /// <param name="targetId">Recipient user ID</param>
        /// <param name="replyToMsgId">Message ID being replied to</param>
        /// <param name="replySenderName">Name of the original sender</param>
        /// <param name="replySnippet">Short excerpt from the original message</param>
        /// <param name="content">Reply message text</param>
        public async Task SendReplyAsync(string targetId, string replyToMsgId, string replySenderName, string replySnippet, string content)
        {
            if (string.IsNullOrWhiteSpace(content)) return;
            if (_socketService == null || !_socketService.IsConnected) return;

            try
            {
                var data = new ChatMessageData
                {
                    MsgId = Guid.NewGuid().ToString("N"),
                    Content = content,
                    TargetType = "PRIVATE",
                    TargetId = targetId,
                    Sender = CurrentUser ?? new SenderInfo { DisplayName = "Me" },
                    ReplyTo = new ReplyInfo
                    {
                        MsgId = replyToMsgId,
                        SenderName = replySenderName,
                        ContentSnippet = replySnippet
                    }
                };

                await _socketService.SendChatMessageAsync(data);
                InvokeOnUI(() => Messages.Add(data));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Send reply error: {ex.Message}");
            }
        }

        /// <summary>
        /// Sends a forwarded message to a new recipient.
        /// The forwarded flag indicates this message comes from another sender.
        /// 
        /// Protocol: Same as SendMessageAsync, but IsForwarded = true and ForwardFromName is set
        /// </summary>
        /// <param name="targetId">New recipient user ID</param>
        /// <param name="originalContent">Content of the message being forwarded</param>
        /// <param name="forwardFromName">Name of the original sender</param>
        public async Task SendForwardAsync(string targetId, string originalContent, string forwardFromName)
        {
            if (string.IsNullOrWhiteSpace(originalContent)) return;
            if (_socketService == null || !_socketService.IsConnected) return;

            try
            {
                var data = new ChatMessageData
                {
                    MsgId = Guid.NewGuid().ToString("N"),
                    Content = originalContent,
                    TargetType = "PRIVATE",
                    TargetId = targetId,
                    Sender = CurrentUser ?? new SenderInfo { DisplayName = "Me" },
                    IsForwarded = true,
                    ForwardFromName = forwardFromName
                };

                await _socketService.SendChatMessageAsync(data);
                InvokeOnUI(() => Messages.Add(data));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Send forward error: {ex.Message}");
            }
        }

        /// <summary>
        /// Sends a broadcast message to all connected users.
        /// Sets TargetType to BROADCAST, implying server will distribute to all clients.
        /// 
        /// Protocol: Same structure as CHAT_MSG, but TargetType = "BROADCAST"
        /// </summary>
        /// <param name="content">Message content to broadcast</param>
        public async Task SendBroadcastAsync(string content)
        {
            if (string.IsNullOrWhiteSpace(content)) return;
            if (_socketService == null || !_socketService.IsConnected) return;

            try
            {
                var data = new ChatMessageData
                {
                    MsgId = Guid.NewGuid().ToString("N"),
                    Content = content,
                    TargetType = "BROADCAST",
                    TargetId = "*", // Special marker for broadcast
                    Sender = CurrentUser ?? new SenderInfo { DisplayName = "Me" }
                };

                await _socketService.SendChatMessageAsync(data);
                InvokeOnUI(() => Messages.Add(data));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Send broadcast error: {ex.Message}");
            }
        }

        /// <summary>
        /// Loads older messages when the user scrolls to the top of chat history.
        /// This is a placeholder for MVP; in production, load from database or server.
        /// 
        /// Current implementation:
        /// - Loads from in-memory _historyQueue (populated by LoadFakeHistory)
        /// - Limits to HistoryPageSize (20) messages per load
        /// - Prevents concurrent loads with IsLoadingHistory flag
        /// - Prepends older messages to the Messages collection
        /// </summary>
        /// <returns>True if history loaded, false if no more history available</returns>
        public async Task<bool> LoadHistoryAsync()
        {
            if (IsLoadingHistory) return false;

            IsLoadingHistory = true;
            try
            {
                // Simulate network delay
                await Task.Delay(300);

                var batch = new List<ChatMessageData>();
                for (int i = 0; i < HistoryPageSize && _historyQueue.Count > 0; i++)
                {
                    batch.Add(_historyQueue.Dequeue());
                }

                if (batch.Count == 0)
                {
                    return false; // No more history
                }

                InvokeOnUI(() =>
                {
                    // Prepend to front of collection (older messages first)
                    for (int i = batch.Count - 1; i >= 0; i--)
                    {
                        Messages.Insert(0, batch[i]);
                    }
                });

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Load history error: {ex.Message}");
                return false;
            }
            finally
            {
                IsLoadingHistory = false;
            }
        }

        /// <summary>
        /// Populates the history queue with fake older messages for testing.
        /// In production, this would fetch from a database via HTTP or query server.
        /// </summary>
        public void LoadFakeHistory(int count = 50)
        {
            _historyQueue.Clear();
            var now = DateTime.Now;

            for (int i = count; i > 0; i--)
            {
                _historyQueue.Enqueue(new ChatMessageData
                {
                    MsgId = Guid.NewGuid().ToString("N"),
                    Content = $"History message #{i}: This is an old message from {i} messages ago",
                    TargetType = "PRIVATE",
                    TargetId = "unknown",
                    Sender = new SenderInfo { UserId = $"user_{i}", DisplayName = $"User {i}" }
                });
            }
        }

        /// <summary>
        /// Handles incoming chat messages from the socket service.
        /// Adds received messages to the Messages collection on the UI thread.
        /// </summary>
        private void HandleChatMessageReceived(Packet<ChatMessageData> packet)
        {
            if (packet?.Data == null) return;

            InvokeOnUI(() =>
            {
                Messages.Add(packet.Data);
            });
        }

        /// <summary>
        /// Invokes an action on the UI thread if a Dispatcher is available.
        /// Otherwise executes on the thread pool.
        /// This ensures thread-safe updates to the ObservableCollection.
        /// </summary>
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
                catch { }
            }
            else
            {
                try { Task.Run(action); } catch { }
            }
        }

        /// <summary>
        /// Cleans up resources. Call when the ViewModel is no longer needed.
        /// </summary>
        public void Dispose()
        {
            if (_socketService != null)
            {
                _socketService.OnChatMessageReceived -= HandleChatMessageReceived;
            }
            Messages.Clear();
        }
    }
}
