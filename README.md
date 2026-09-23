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

## 💻 Yêu cầu môi trường

- **Hệ điều hành:** Windows 10/11 (bắt buộc — Client dùng WPF, chỉ chạy trên Windows: `net10.0-windows`). Riêng project Server có thể build/chạy trên bất kỳ OS nào hỗ trợ .NET 10 (Windows/Linux/macOS).
- **.NET 10 SDK** trở lên.
- **Visual Studio 2022** (bản mới nhất, đã cài workload ".NET desktop development" để build được WPF).
- **NuGet packages** (tự động khôi phục khi build, không cần cài thủ công):
  - `Microsoft.Data.Sqlite` — Server dùng SQLite để lưu tài khoản người dùng.
  - `BCrypt.Net-Next` — mã hoá mật khẩu.
- Không cần cài SQL Server/MySQL riêng: Database SQLite (`ChatApp.db`) được Server tự tạo khi chạy lần đầu.
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
---

## 🏗️ Kiến trúc hệ thống

Dự án theo mô hình **Client – Server** giao tiếp qua **TCP Socket**, gồm 3 project tách biệt:

- **Server** (Console App): mở `TcpListener` tại cổng `8888`, mỗi client kết nối được xử lý trên 1 Thread nền riêng (đồng thời nhiều client). Gồm các thành phần chính:
  - `ChatServer` — Accept kết nối mới.
  - `AuthHandler` — xử lý Login/Register, sau đó lắng nghe tin nhắn liên tục từ client đã đăng nhập.
  - `ClientManager` — quản lý danh sách client đang online (thread-safe), phát thông báo Online/Offline real-time.
  - `MessageRouter` — định tuyến tin nhắn PRIVATE tới đúng người nhận, xử lý Broadcast.
  - `HeartbeatMonitor` — quét định kỳ, tự động gỡ client bị rớt mạng đột ngột.
  - `Data` — kết nối & thao tác SQLite (lưu tài khoản người dùng).
- **Client** (WPF, kiến trúc **MVVM**): giao diện Đăng nhập/Đăng ký, Danh sách liên hệ, Chat (Reply/Forward/Emoji/Avatar), giao tiếp Server qua `ClientSocketService`.
- **Common**: dùng chung cho cả Server & Client — định nghĩa `Packet<T>`/model dữ liệu (`ChatModels.cs`) và `MessageProtocol.cs` (đóng/mở khung gói tin Length-Prefixed Frame).

**Luồng dữ liệu:** Client mở kết nối TCP tới Server → gửi gói `AUTH_REQ` (Login/Register, JSON) → Server xác thực, trả `AUTH_RESPONSE` → nếu thành công, Server chuyển sang vòng lặp lắng nghe `CHAT_MSG`/`GET_USERS`/`UPDATE_AVATAR` từ client đó → `MessageRouter` định tuyến tin nhắn tới người nhận (PRIVATE) hoặc toàn bộ client online (Broadcast) → mỗi thay đổi Online/Offline được `ClientManager` phát `USER_STATUS_NOTIFY` cho các client còn lại.
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


## ⚙️ Cấu hình

Hiện dự án chưa dùng file config riêng (appsettings/.env) — các giá trị cấu hình đang khai báo trực tiếp trong code:

| Cấu hình | Nơi khai báo | Giá trị mặc định |
| --- | --- | --- |
| Cổng lắng nghe của Server | `Code/Server/Networking/ChatServer.cs` (`ServerPort`) | `8888` |
| IP/Port Server mà Client kết nối tới | `Code/Client/Views/LoginView.xaml.cs`, `Code/Client/Views/RegisterView.xaml.cs` | `127.0.0.1 : 8888` |
| Đường dẫn Database SQLite | `Code/Server/Data/DbConnectionFactory.cs` (`DbPath`) | `<thư mục chạy Server>/ChatApp.db` (tự tạo khi Initialize lần đầu) |
| Schema khởi tạo Database | `Code/Server/Data/Schema.sql` | Tự nạp khi Server khởi động lần đầu |

**Muốn đổi cổng hoặc chạy Client kết nối tới Server ở máy khác (LAN):**
1. Đổi `ServerPort` trong `ChatServer.cs` nếu muốn đổi cổng Server.
2. Sửa IP/port trong `LoginView.xaml.cs` và `RegisterView.xaml.cs` (thành IP LAN của máy chạy Server), build lại Client.
3. Đảm bảo Firewall Windows cho phép cổng đã chọn.

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
