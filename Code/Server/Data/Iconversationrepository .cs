using System.Collections.Generic;

namespace ChatTCP.Server.Data
{
    ///Đại diện cho 1 dòng dữ liệu trong bảng Conversations
    public class ConversationModel
    {
        public int ConversationId { get; set; }
        public bool IsGroup { get; set; }
        public string Name { get; set; }
    }

    ///Hợp đồng thao tác với bảng Conversations và ConversationMembers
    public interface IConversationRepository
    {
        ///Tạo hội thoại mới (1-1 hoặc nhóm), tự thêm các thành viên ban đầu. Trả về ConversationId
        int CreateConversation(bool isGroup, string name, IEnumerable<int> memberUserIds);

        ///Thêm 1 thành viên vào hội thoại đã có sẵn
        void AddMember(int conversationId, int userId);

        ///Lấy danh sách UserId là thành viên của 1 cuộc hội thoại
        List<int> GetMemberUserIds(int conversationId);

        ///Lấy danh sách hội thoại mà 1 user đang tham gia
        List<ConversationModel> GetConversationsByUser(int userId);
    }
}