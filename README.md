# UDM08 - TCP Chat

Ứng dụng Chat Client - Server sử dụng giao thức **TCP** được phát triển bằng **C# / .NET (WPF)** trên **Visual Studio**.

## 📌 Giới thiệu

Đây là dự án nhóm thực hiện đề tài **UDM08 - TCP Chat**.

Ứng dụng cho phép nhiều người dùng kết nối đến Server và trò chuyện với nhau theo mô hình **Client - Server** thông qua **TCP Socket**, dữ liệu trao đổi ở định dạng **JSON** với cơ chế **Length-Prefixed Framing** để chống dính/xé gói tin.

---

## 🛠️ Công nghệ sử dụng

- C# / .NET 10
- WPF (Windows Presentation Foundation) — kiến trúc **MVVM**
- TCP Socket (`TcpListener` / `TcpClient`, `NetworkStream`)
- JSON (`System.Text.Json`)
- SQL (lưu trữ thông tin người dùng)
- Git & GitHub
- Visual Studio 2022

---

## ✨ Chức năng

- Đăng ký / Đăng nhập (Auth)
- Kết nối Client - Server qua TCP Socket
- Chat tin nhắn riêng tư , nhắn được những người đang online
- Reply & Forward tin nhắn
- Hiển thị Emoji
- Hiển thị Avatar
- Hiển thị trạng thái Online/Offline (theo thời gian thực)
- Duy trì kết nối bằng cơ chế Heartbeat (Ping/Pong)

---

## 👥 Thành viên & Phân công

| STT | Thành viên | Module / Chức năng                                                 |
| --- | ---------- | -------------------------------------------------------------------|
| 1   | Kiên       | TCP Server – Connection Listener & Client Manager, Auth + Database |
| 2   | Khương     | Concurrent Client Handling & Disconnect/Error Handling             |
| 3   | Thanh Thuý | Message Protocol & Message Routing                                 |
| 4   | Nam Lâm    | Async Client Networking & GUI (Danh sách/Avatar)                   |
| 5   | Minh Phước | Async Client Networking & GUI (Chat)                               |
| 6   |            |                                                                    |

---

## 📂 Cấu trúc dự án

```
UDM08-TCP-Chat/
├── Code/
│   ├── TCP-Chat-Sub.slnx
│   │
│   ├── Server/
│   │   ├── Main.cs
│   │   ├── MessageRouter.cs
│   │   ├── Networking/
│   │   │   ├── ChatServer.cs        # TcpListener, Accept loop, port 8888
│   │   │   └── ClientSession.cs
│   │   ├── Services/
│   │   │   ├── AuthHandler.cs
│   │   │   ├── ClientManager.cs
│   │   │   └── HeartbeatMonitor.cs
│   │   └── Data/
│   │       ├── Schema.sql
│   │       ├── DbConnectionFactory.cs
│   │       ├── IUserRepository.cs
│   │       └── UserRepository.cs
│   │
│   ├── Client/                       # Ứng dụng WPF (MVVM)
│   │   ├── App.xaml / App.xaml.cs
│   │   ├── MainWindow.xaml / .cs
│   │   ├── Service/
│   │   │   ├── ClientSocketService.cs
│   │   │   └── ConversationStore.cs
│   │   ├── ViewModel/
│   │   │   └── ChatViewModel.cs
│   │   ├── Views/
│   │   │   ├── LoginView.xaml
│   │   │   ├── RegisterView.xaml
│   │   │   ├── ContactListView.xaml
│   │   │   └── ChatView.xaml
│   │   └── Assets/                   # Theme, Fonts
│   │
│   └── Common/                        # Dùng chung Client & Server
│       ├── ChatModels.cs
│       └── MessageProtocol.cs        # Đóng/mở khung gói tin (Length-Prefixed Frame)
│
├── DOCX/
│   └── protocol.md                    # Đặc tả chi tiết giao thức (packet types, format JSON, mã lỗi)
├── PPTX/
└── Extra/
```

---

## 📡 Giao thức kết nối (tóm tắt)

- **Protocol:** TCP | **Port mặc định:** `8888` | **Định dạng dữ liệu:** JSON (UTF-8)
- **Frame format:** `4 byte độ dài (Big-Endian) + Payload JSON`
- **Các loại gói tin chính:** `AUTH_REQ/RES`, `CHAT_MSG`, `USER_STATUS_NOTIFY`, `HEARTBEAT_PING/PONG`, `ERROR`

Chi tiết đầy đủ xem tại [`DOCX/protocol.md`](./DOCX/protocol.md).

---

## 🚀 Bắt đầu

Clone dự án:

```bash
git clone https://github.com/ddkien05/UDM08-TCP-Chat.git
cd UDM08-TCP-Chat
```

Mở file **Code/TCP-Chat-Sub.slnx** bằng **Visual Studio 2022** (yêu cầu .NET 10 SDK).

Chạy project **Server** trước, sau đó chạy project **Client** để kết nối tới `127.0.0.1:8888`.

---

## 🌿 Quy trình làm việc

Tạo branch:

```bash
git checkout -b feature/ten-chuc-nang
```

Sau khi hoàn thành:

```bash
git add .
git commit -m "Mo ta chuc nang"
git push origin feature/ten-chuc-nang
```

Sau đó tạo **Pull Request** để merge vào `dev`.
