namespace ChatTCP.Server.Data
{
    /// Đại diện cho 1 dòng dữ liệu trong bảng Users.
    public class UserModel
    {
        public int UserId { get; set; }
        public string Username { get; set; }
        public string PasswordHash { get; set; }
        public string DisplayName { get; set; }
        public string AvatarUrl { get; set; }
    }

    /// Hợp đồng thao tác với bảng Users.
    public interface IUserRepository
    {
        ///Tìm user theo Username. Trả về null nếu không tồn tại.
        UserModel GetByUsername(string username);

        ///Tìm user theo UserId. Trả về null nếu không tồn tại
        UserModel GetById(int userId);

        //Tạo user mới, trả về UserId vừa tạo
        int CreateUser(string username, string passwordHash, string displayName);

        ///Cập nhật đường dẫn avatar của 1 user.
        void UpdateAvatar(int userId, string avatarUrl);

        ///Đánh dấu 1 user đang online hay offline
        void SetOnlineStatus(int userId, bool isOnline);
    }
}