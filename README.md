# UDM08 - TCP Chat

Ứng dụng chat Client-Server sử dụng giao thức TCP.

## 📌 Giới thiệu

Đây là dự án nhóm thực hiện đề tài UDM_08:

> Xây dựng ứng dụng Chat Client-Server sử dụng giao thức TCP.

Ứng dụng cho phép nhiều người dùng kết nối đến Server và trò chuyện với nhau thông qua giao diện đồ họa.

## 🛠️ Công nghệ sử dụng

- C#
- .NET
- Windows Forms (WinForms)
- TCP Socket
- Git & GitHub
- JSON (định dạng dữ liệu tin nhắn)

## ✨ Chức năng chính

- Kết nối Client - Server thông qua TCP
- Chat giữa nhiều người dùng
- Hiển thị danh sách người dùng/liên hệ
- Hiển thị avatar
- Gửi và nhận tin nhắn
- Reply tin nhắn
- Forward tin nhắn
- Gửi và hiển thị Emoji
- Hiển thị trạng thái người dùng
- Xử lý kết nối và ngắt kết nối

## 👥 Phân công thành viên

| Thành viên | Nhiệm vụ |
|---|---|
| Thành viên 1 | TCP Server và quản lý Client |
| Thành viên 2 | TCP Client và gửi/nhận tin nhắn |
| Thành viên 3 | Giao diện WinForms và danh sách liên hệ |
| Thành viên 4 | Chức năng Reply và Forward |
| Thành viên 5 | Emoji và xử lý hiển thị tin nhắn |
| Thành viên 6 | Tích hợp, kiểm thử, báo cáo và demo |

## 📂 Cấu trúc dự án

```text
UDM08-TCP-Chat/
│
├── Server/
│   └── TCP Chat Server
│
├── Client/
│   └── TCP Chat Client
│
├── Shared/
│   └── Models
│   └── Message Protocol
│
├── README.md
└── .gitignore

Sau khi được mời, thành viên làm:
git clone link-repository
cd UDM08-TCP-Chat

Sau đó tạo branch riêng:
VD:sever
git checkout -b feature/tcp-server

Làm code xong:

git add .
git commit -m "Add TCP Server"
git push origin feature/tcp-server
Sau đó tạo Pull Request
