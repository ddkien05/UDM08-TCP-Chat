using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using ChatTCP.Common.Models;

namespace ChatTCP.Client.Networking
{
	/// <summary>
	/// Tóm tắt 1 cuộc hội thoại: nội dung + thời điểm tin nhắn gần nhất, và số tin chưa đọc.
	/// </summary>
	public class ConversationSummary
	{
		public string LastMessage { get; set; } = string.Empty;
		public DateTime LastMessageAt { get; set; } = DateTime.MinValue;
		public bool LastMessageIsMine { get; set; }
		public int UnreadCount { get; set; }
	}

	/// <summary>
	/// Lưu tóm tắt hội thoại (tin nhắn gần nhất + chưa đọc) cho từng liên hệ.
	/// 
	/// QUAN TRỌNG: đây là 1 singleton sống xuyên suốt phiên đăng nhập, KHÔNG gắn với
	/// ContactListView hay ChatView cụ thể nào — vì 2 View này bị hủy/tạo lại liên tục
	/// mỗi khi điều hướng qua lại (xem MainWindow.ShowContactList/ShowChatView).
	/// Nếu lưu state này trong ContactListView, mỗi lần quay lại danh sách chat sẽ mất
	/// hết tin nhắn mới nhất vừa nhận lúc đang ở màn hình chat khác.
	/// 
	/// ClientSocketService gọi RecordIncoming/RecordOutgoing ngay khi gói tin CHAT_MSG
	/// đi qua (bất kể View nào đang hiển thị), nên danh sách chat luôn có dữ liệu mới
	/// nhất, đúng kiểu real-time như Messenger.
	/// </summary>
	public class ConversationStore
	{
		public static ConversationStore Instance { get; } = new ConversationStore();

		private readonly ConcurrentDictionary<string, ConversationSummary> _summaries = new();

		/// <summary>
		/// UserId của cuộc chat đang được mở xem (nếu có). Dùng để KHÔNG tính "chưa đọc"
		/// cho đúng cuộc trò chuyện người dùng đang nhìn thấy trên màn hình.
		/// </summary>
		public string? ActiveChatUserId { get; set; }

		/// <summary>Bắn ra (userId, summary) mỗi khi có cập nhật, để ContactListView refresh dòng tương ứng.</summary>
		public event Action<string, ConversationSummary>? Updated;

		public ConversationSummary? Get(string userId) =>
			!string.IsNullOrWhiteSpace(userId) && _summaries.TryGetValue(userId, out var s) ? s : null;

		/// <summary>Ghi nhận tin nhắn mình vừa gửi đi (có thể gửi cho nhiều người cùng lúc, phân tách bởi dấu phẩy).</summary>
		public void RecordOutgoing(string targetId, string content, DateTime at, ChatMessageData? message = null)
		{
			foreach (var id in SplitTargets(targetId))
			{
				var summary = _summaries.GetOrAdd(id, _ => new ConversationSummary());
				summary.LastMessage = content;
				summary.LastMessageAt = at;
				summary.LastMessageIsMine = true;

                if (message != null) AddMessage(id, message);
                Updated?.Invoke(id, summary);
            }
		}

		/// <summary>Ghi nhận tin nhắn vừa nhận được từ 1 người khác. Tự tăng số chưa đọc nếu không phải đang mở đúng cuộc chat đó.</summary>
		public void RecordIncoming(string fromUserId, string content, DateTime at, ChatMessageData? message = null)
		{
			if (string.IsNullOrWhiteSpace(fromUserId)) return;

			var summary = _summaries.GetOrAdd(fromUserId, _ => new ConversationSummary());
			summary.LastMessage = content;
			summary.LastMessageAt = at;
			summary.LastMessageIsMine = false;

			if (!string.Equals(fromUserId, ActiveChatUserId, StringComparison.Ordinal))
			{
				summary.UnreadCount++;
			}

            if (message != null) AddMessage(fromUserId, message);
            Updated?.Invoke(fromUserId, summary);
        }

		/// <summary>Đánh dấu đã đọc toàn bộ tin nhắn của 1 liên hệ (gọi khi mở ChatView với người đó).</summary>
		public void MarkRead(string userId)
		{
			if (string.IsNullOrWhiteSpace(userId)) return;

			if (_summaries.TryGetValue(userId, out var summary) && summary.UnreadCount != 0)
			{
				summary.UnreadCount = 0;
				Updated?.Invoke(userId, summary);
			}
		}

		private static IEnumerable<string> SplitTargets(string targetId) =>
			(targetId ?? string.Empty)
				.Split(',', StringSplitOptions.RemoveEmptyEntries)
				.Select(x => x.Trim())
				.Where(x => x.Length > 0);
        private readonly ConcurrentDictionary<string, List<ChatMessageData>> _messages = new();

        public void AddMessage(string peerUserId, ChatMessageData message)
        {
            if (string.IsNullOrWhiteSpace(peerUserId) || message == null) return;

            var list = _messages.GetOrAdd(peerUserId, _ => new List<ChatMessageData>());
            lock (list)
            {
                if (!string.IsNullOrEmpty(message.MsgId) && list.Any(m => m.MsgId == message.MsgId)) return;
                list.Add(message);
            }
        }

        public List<ChatMessageData> GetMessages(string peerUserId)
        {
            if (!_messages.TryGetValue(peerUserId ?? string.Empty, out var list))
                return new List<ChatMessageData>();

            lock (list) { return new List<ChatMessageData>(list); }
        }

        public void Clear()
        {
            _messages.Clear();
            _summaries.Clear();
        }
    }
}