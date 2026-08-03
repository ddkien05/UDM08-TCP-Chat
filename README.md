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
| Thành viên 1 |  |
| Thành viên 2 |  |
| Thành viên 3 |  |
| Thành viên 4 |  |
| Thành viên 5 |  |
| Thành viên 6 |  |

## 📂 Cấu trúc dự án

```
ChatTCP/
│
├── .gitignore                  # Danh sách file/thư mục được Git bỏ qua
│
├── ChatTCP.Client/             # Ứng dụng Client (WPF)
│   ├── Dependencies/           # Thư viện và package sử dụng
│   ├── App.xaml                # Điểm khởi tạo ứng dụng WPF
│   ├── AssemblyInfo.cs         # Thông tin Assembly
│   └── MainWindow.xaml         # Giao diện chính của Client
│
├── ChatTCP.Common/             # Thư viện dùng chung giữa Client và Server
│   ├── Dependencies/
│   └── Class1.cs               # Chứa các lớp/model dùng chung (sẽ mở rộng sau)
│
├── ChatTCP.Server/             # Ứng dụng Server Console
│   ├── Dependencies/           # Thư viện và package sử dụng
│   └── Program.cs              # Điểm khởi chạy Server
│
└── ChatTCP.sln                 # File Solution của Visual Studio
```


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



🔄 Quy trình làm việc hằng ngày

Trước khi bắt đầu code:

git checkout main
git pull origin main
git checkout ten-branch-cua-ban
git merge main

Sau khi code xong:

git add .
git commit -m "Mo ta chuc nang"
git push origin ten-branch-cua-ban

Sau đó tạo Pull Request.
