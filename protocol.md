# ĐẶC TẢ GIAO THỨC (PROTOCOL SPECIFICATION)

Tài liệu này định nghĩa chi tiết giao thức giao tiếp tầng ứng dụng (Application Layer Protocol) giữa **Client** và **Server** trong ứng dụng **UDM08-TCP-Chat**. Giao thức được triển khai trên nền tảng **C# / .NET** sử dụng kết nối **TCP** và định dạng dữ liệu **JSON**.

---

## 1. Tổng quan và Giới thiệu

### 1.1. Mục đích
Giao thức này đảm bảo việc truyền tải thông tin giữa Client và Server diễn ra tin cậy, toàn vẹn, không bị mất mát dữ liệu, đồng thời giải quyết triệt để các vấn đề dính gói (Packet Coalescing) và phân mảnh/vỡ gói (Packet Fragmentation) đặc trưng của dòng truyền dẫn TCP (Byte Stream).

### 1.2. Phạm vi ứng dụng
Áp dụng cho toàn bộ các module kết nối mạng thuộc giải pháp (Solution) bao gồm:
*   `ChatTCP.Common` (Định nghĩa chung)
*   `ChatTCP.Server` (Xử lý Socket máy chủ)
*   `ChatTCP.Client` (Xử lý Socket máy trạm và Giao diện)

### 1.3. Thuật ngữ viết tắt
*   **TCP**: Transmission Control Protocol (Giao thức điều khiển truyền dẫn).
*   **JSON**: JavaScript Object Notation (Định dạng trao đổi dữ liệu gọn nhẹ).
*   **Opcode**: Operation Code (Mã lệnh xác định loại gói tin).
*   **Header**: Phần đầu gói tin có kích thước cố định.
*   **Payload**: Phần thân gói tin chứa nội dung dữ liệu thực tế.

---

## 2. Kiến trúc và Mô hình giao tiếp

*   **Mô hình kết nối**: Client - Server (Khách - Chủ).
*   **Cổng mạng mặc định (Port)**: `8888`.
*   **Thứ tự truyền Byte (Byte Order)**: Thống nhất sử dụng **Big-Endian** (Network Byte Order) để đảm bảo tính nhất quán khi chuyển đổi mảng byte giữa các thiết bị phần cứng khác nhau. Trong C#, sử dụng các hàm chuyển đổi như `IPAddress.HostToNetworkOrder` hoặc các hàm tự viết để xử lý byte header.

---

### 3. Cấu trúc gói tin vật lý (Packet Frame Format)

Mọi gói tin truyền qua mạng Socket bắt buộc phải được đóng khung (Framing) theo cấu trúc dưới đây để bên nhận có ranh giới phân tách dữ liệu rõ ràng:

| Phân đoạn | Trường dữ liệu (Field) | Kích thước (Size) | Giá trị mẫu |
| :--- | :--- | :--- | :--- |
| **HEADER** | Magic Bytes | 2 Bytes | `0xAA 0xBB` |
| **HEADER** | MessageType / Opcode | 2 Bytes | `0x00 0x01` |
| **HEADER** | Length (Độ dài Payload) | 4 Bytes | `0x00 0x00 0x14` (Thập phân: 20 bytes) |
| **PAYLOAD** | JSON Data | N Bytes | `{"username": "abc", ... }` |

### 3.1. Chi tiết phần Header (Cố định 8 Bytes)
*   **Magic Bytes (2 Bytes - `ushort`)**: Giá trị cố định `0xAABB`. Dùng để kiểm tra tính hợp lệ của gói tin thuộc ứng dụng. Nếu nhận được byte đầu khác giá trị này, kết nối sẽ bị ngắt lập tức để bảo vệ hệ thống.
*   **MessageType (2 Bytes - `ushort` / Enum)**: Mã Opcode xác định loại nghiệp vụ của gói tin để định tuyến xử lý nhanh trước khi giải mã JSON.
*   **Length (4 Bytes - `uint`)**: Độ dài tính bằng Byte của phần Payload ở phía sau.

### 3.2. Chi tiết phần Payload (Kích thước biến đổi)
*   **Định dạng**: Chuỗi JSON được mã hóa bằng chuẩn **UTF-8**.
*   **Kích thước**: Đúng bằng giá trị lưu tại trường **Length** trong Header.

---

## 4. Danh sách mã MessageType (Opcodes)

Trong mã nguồn `Common`, loại tin nhắn được ánh xạ qua cấu trúc Enum tương đương:

| Opcode (Hex) | Opcode (Dec) | Tên loại gói tin   | Ý nghĩa nghiệp vụ                                   |
| :----------- | :----------- | :----------------- | :-------------------------------------------------- |
| `0x0001`     | 1            | `LOGIN_REQ`        | Yêu cầu đăng nhập từ Client lên Server.             |
| `0x0002`     | 2            | `LOGIN_RES`        | Phản hồi kết quả đăng nhập từ Server về Client.     |
| `0x0003`     | 3            | `STATUS_UPDATE`    | Thông báo trạng thái Online/Offline của tài khoản.  |
| `0x0004`     | 4            | `LOGOUT_REQ`       | Yêu cầu đăng xuất / Ngắt kết nối an toàn từ Client. |
| `0x0010`     | 16           | `CHAT_MSG_SEND`    | Gói gửi tin nhắn thường của Client lên Server.      |
| `0x0011`     | 17           | `CHAT_MSG_RECV`    | Server phân phối tin nhắn thường tới Client đích.   |
| `0x0012`     | 18           | `CHAT_REPLY`       | Gói tin nhắn có kèm ID phản hồi.                    |
| `0x0013`     | 19           | `CHAT_FORWARD`     | Gói tin nhắn chuyển tiếp.                           |
| `0x0014`     | 20           | `CREATE_GROUP_REQ` | Yêu cầu tạo phòng chat nhóm từ Client.              |
| `0x0015`     | 21           | `CREATE_GROUP_RES` | Phản hồi thông tin nhóm mới được tạo từ Server.     |
| `0x0020`     | 32           | `HEARTBEAT`        | Gói tin rỗng duy trì kết nối định kỳ (Ping/Pong).   |

---

## 5. Đặc tả chi tiết dữ liệu JSON trong Payload

Mọi thuộc tính trong payload JSON được thống nhất viết theo chuẩn **camelCase**.

### 5.1. Đăng nhập (`LOGIN_REQ` - Opcode `1`)
*   **Hướng gửi**: Client → Server

```json
{
  "username": "kien_dd05",
  "passwordHash": "8c6976e5b5410415bde908bd4dee15dfb167a9c873fc4bb8a81f6f2ab448a918" 
}
```

### 5.2. Phản hồi Đăng nhập (`LOGIN_RES` - Opcode `2`)
*   **Hướng gửi**: Server → Client

```json
{
  "isSuccess": true,
  "message": "Đăng nhập thành công",
  "userId": "USR_001",
  "displayName": "Đỗ Duy Kiên",
  "avatarUrl": "http://server/avatars/user_001.png"
}
```

### 5.3. Trạng thái hoạt động (`STATUS_UPDATE` - Opcode `3`)
*   **Hướng gửi**: Server → Tất cả các Client đang online khác

```json
{
  "userId": "USR_001",
  "status": "ONLINE" // Hoặc "OFFLINE"
}
```

### 5.4. Đăng xuất (`LOGOUT_REQ` - Opcode `4`)
*   **Hướng gửi**: Client → Server

```json
{
  "userId": "USR_001",
  "reason": "User closed the application"
}
```

### 5.5. Gửi Tin nhắn thường (`CHAT_MSG_SEND` - Opcode `16`)
*   **Hướng gửi**: Client → Server

```json
{
  "receiverId": "USR_002", // Có thể là User ID hoặc Group ID
  "isGroup": false,
  "content": "Chào bạn, hôm nay thế nào?"
}
```

### 5.6. Phân phối Tin nhắn (`CHAT_MSG_RECV` - Opcode `17`)
*   **Hướng gửi**: Server → Client nhận

```json
{
  "messageId": "MSG_100249",
  "senderId": "USR_001",
  "senderName": "Đỗ Duy Kiên",
  "receiverId": "USR_002",
  "isGroup": false,
  "content": "Chào bạn, hôm nay thế nào?",
  "timestamp": "2026-08-09T13:30:00Z"
}
```

### 5.7. Phản hồi Tin nhắn (`CHAT_REPLY` - Opcode `18`)
*   **Hướng gửi**: Client → Server → Client nhận

```json
{
  "messageId": "MSG_100250",
  "senderId": "USR_002",
  "receiverId": "USR_001",
  "replyToMessageId": "MSG_100249", // ID của tin nhắn gốc được phản hồi
  "originalContent": "Chào bạn, hôm nay thế nào?", // Dùng hiển thị nhanh trên UI
  "content": "Mình khỏe, cảm ơn bạn đã hỏi thăm!",
  "timestamp": "2026-08-09T13:31:00Z"
}
```

### 5.8. Chuyển tiếp Tin nhắn (`CHAT_FORWARD` - Opcode `19`)
*   **Hướng gửi**: Client → Server

```json
{
  "originalMessageId": "MSG_100249",
  "content": "Chào bạn, hôm nay thế nào?",
  "targetIds": ["USR_003", "USR_004", "GRP_012"] // Danh sách các đích nhận tin nhắn
}
```

### 5.9. Yêu cầu tạo nhóm (`CREATE_GROUP_REQ` - Opcode `20`)
*   **Hướng gửi**: Client → Server

```json
{
  "groupName": "Nhóm Học Tập C#",
  "memberIds": ["USR_001", "USR_002", "USR_003"]
}
```

### 5.10. Phản hồi tạo nhóm (`CREATE_GROUP_RES` - Opcode `21`)
*   **Hướng gửi**: Server → Các thành viên được chỉ định trong nhóm

```json
{
  "isSuccess": true,
  "groupId": "GRP_012",
  "groupName": "Nhóm Học Tập C#",
  "memberIds": ["USR_001", "USR_002", "USR_003"],
  "timestamp": "2026-08-09T13:35:00Z"
}
```

---

## 6. Luồng nghiệp vụ cơ bản (Workflow)

```text
Client (Gửi yêu cầu)              Server (Định tuyến xử lý)             Client (Nhận thông tin)
      |                                      |                                      |
      |====== (1) Gửi LOGIN_REQ ===========>|                                      |
      |                                      |-- Xác thực thông tin --              |
      |<===== (2) Phản hồi LOGIN_RES =======|                                      |
      |                                      |                                      |
      |                                      |====== (3) STATUS_UPDATE (ONLINE) ===>| (Cập nhật danh bạ)
      |                                      |                                      |
      |====== (4) CHAT_MSG_SEND ===========>|                                      |
      |                                      |-- Lưu DB SQLite / Tìm tuyến --       |
      |                                      |====== (5) CHAT_MSG_RECV ===========>| (Hiển thị bóng chat)
      |                                      |                                      |
```

---

## 7. Xử lý lỗi và Các mã trạng thái (Error Handling)

Khi thực hiện các thao tác xác thực, tìm kiếm, kết nối, các trường hợp lỗi sẽ được trả về trực tiếp trong thuộc tính `message` hoặc `code` tại Payload của các gói tin dạng phản hồi (Response):

| Mã trạng thái | Tên lỗi        | Ý nghĩa thực tế                                                     |
| :------------ | :------------- | :------------------------------------------------------------------ |
| `200`         | `SUCCESS`      | Yêu cầu được thực thi thành công.                                   |
| `400`         | `BAD_REQUEST`  | Định dạng gói tin JSON không hợp lệ hoặc thiếu trường bắt buộc.     |
| `401`         | `UNAUTHORIZED` | Tài khoản hoặc mật khẩu không chính xác.                            |
| `404`         | `NOT_FOUND`    | Đối tượng nhận (User ID hoặc Group ID) không tồn tại.               |
| `409`         | `CONFLICT`     | Tên người dùng hoặc tên nhóm đã bị trùng lặp.                       |
| `500`         | `SERVER_ERROR` | Lỗi phát sinh trong quá trình ghi đọc SQLite hoặc lỗi luồng Server. |

---

## 8. Bảo mật và Xác thực cơ bản

1.  **Xác thực mật khẩu**: Mật khẩu của Client gửi lên Server không được truyền dưới dạng văn bản thô (Plain-text). Client bắt buộc phải băm (hash) bằng giải thuật **SHA-256** trước khi đưa vào trường `passwordHash` trong gói `LOGIN_REQ`.
2.  **Đóng kết nối an toàn (Graceful Shutdown)**: Khi Client ngắt kết nối một cách chủ động (bấm nút Thoát hoặc đóng ứng dụng), Client phải gửi gói tin `LOGOUT_REQ` (Opcode `4`) để Server xóa Socket khỏi danh sách quản lý kết nối (`activeConnections`) và cập nhật trạng thái ngoại tuyến (`OFFLINE`) tới các client khác trước khi chính thức ngắt kết nối TCP vật lý.
```

### 5.3. Trạng thái hoạt động (`STATUS_UPDATE` - Opcode `3`)
*   **Hướng gửi**: Server → Tất cả các Client đang online khác

```json
{
  "userId": "USR_001",
  "status": "ONLINE" // Hoặc "OFFLINE"
}
```

### 5.4. Đăng xuất (`LOGOUT_REQ` - Opcode `4`)
*   **Hướng gửi**: Client → Server

```json
{
  "userId": "USR_001",
  "reason": "User closed the application"
}
```

### 5.5. Gửi Tin nhắn thường (`CHAT_MSG_SEND` - Opcode `16`)
*   **Hướng gửi**: Client → Server

```json
{
  "receiverId": "USR_002", // Có thể là User ID hoặc Group ID
  "isGroup": false,
  "content": "Chào bạn, hôm nay thế nào?"
}
```

### 5.6. Phân phối Tin nhắn (`CHAT_MSG_RECV` - Opcode `17`)
*   **Hướng gửi**: Server → Client nhận

```json
{
  "messageId": "MSG_100249",
  "senderId": "USR_001",
  "senderName": "Đỗ Duy Kiên",
  "receiverId": "USR_002",
  "isGroup": false,
  "content": "Chào bạn, hôm nay thế nào?",
  "timestamp": "2026-08-09T13:30:00Z"
}
```

### 5.7. Phản hồi Tin nhắn (`CHAT_REPLY` - Opcode `18`)
*   **Hướng gửi**: Client → Server → Client nhận

```json
{
  "messageId": "MSG_100250",
  "senderId": "USR_002",
  "receiverId": "USR_001",
  "replyToMessageId": "MSG_100249", // ID của tin nhắn gốc được phản hồi
  "originalContent": "Chào bạn, hôm nay thế nào?", // Dùng hiển thị nhanh trên UI
  "content": "Mình khỏe, cảm ơn bạn đã hỏi thăm!",
  "timestamp": "2026-08-09T13:31:00Z"
}
```

### 5.8. Chuyển tiếp Tin nhắn (`CHAT_FORWARD` - Opcode `19`)
*   **Hướng gửi**: Client → Server

```json
{
  "originalMessageId": "MSG_100249",
  "content": "Chào bạn, hôm nay thế nào?",
  "targetIds": ["USR_003", "USR_004", "GRP_012"] // Danh sách các đích nhận tin nhắn
}
```

### 5.9. Yêu cầu tạo nhóm (`CREATE_GROUP_REQ` - Opcode `20`)
*   **Hướng gửi**: Client → Server

```json
{
  "groupName": "Nhóm Học Tập C#",
  "memberIds": ["USR_001", "USR_002", "USR_003"]
}
```

### 5.10. Phản hồi tạo nhóm (`CREATE_GROUP_RES` - Opcode `21`)
*   **Hướng gửi**: Server → Các thành viên được chỉ định trong nhóm

```json
{
  "isSuccess": true,
  "groupId": "GRP_012",
  "groupName": "Nhóm Học Tập C#",
  "memberIds": ["USR_001", "USR_002", "USR_003"],
  "timestamp": "2026-08-09T13:35:00Z"
}
```

---

## 6. Luồng nghiệp vụ cơ bản (Workflow)

```text
Client (Gửi yêu cầu)              Server (Định tuyến xử lý)             Client (Nhận thông tin)
      |                                      |                                      |
      |====== (1) Gửi LOGIN_REQ ===========>|                                      |
      |                                      |-- Xác thực thông tin --              |
      |<===== (2) Phản hồi LOGIN_RES =======|                                      |
      |                                      |                                      |
      |                                      |====== (3) STATUS_UPDATE (ONLINE) ===>| (Cập nhật danh bạ)
      |                                      |                                      |
      |====== (4) CHAT_MSG_SEND ===========>|                                      |
      |                                      |-- Lưu DB SQLite / Tìm tuyến --       |
      |                                      |====== (5) CHAT_MSG_RECV ===========>| (Hiển thị bóng chat)
      |                                      |                                      |
```

---

## 7. Xử lý lỗi và Các mã trạng thái (Error Handling)

Khi thực hiện các thao tác xác thực, tìm kiếm, kết nối, các trường hợp lỗi sẽ được trả về trực tiếp trong thuộc tính `message` hoặc `code` tại Payload của các gói tin dạng phản hồi (Response):

| Mã trạng thái | Tên lỗi        | Ý nghĩa thực tế                                                     |
| :------------ | :------------- | :------------------------------------------------------------------ |
| `200`         | `SUCCESS`      | Yêu cầu được thực thi thành công.                                   |
| `400`         | `BAD_REQUEST`  | Định dạng gói tin JSON không hợp lệ hoặc thiếu trường bắt buộc.     |
| `401`         | `UNAUTHORIZED` | Tài khoản hoặc mật khẩu không chính xác.                            |
| `404`         | `NOT_FOUND`    | Đối tượng nhận (User ID hoặc Group ID) không tồn tại.               |
| `409`         | `CONFLICT`     | Tên người dùng hoặc tên nhóm đã bị trùng lặp.                       |
| `500`         | `SERVER_ERROR` | Lỗi phát sinh trong quá trình ghi đọc SQLite hoặc lỗi luồng Server. |

---

## 8. Bảo mật và Xác thực cơ bản

1.  **Xác thực mật khẩu**: Mật khẩu của Client gửi lên Server không được truyền dưới dạng văn bản thô (Plain-text). Client bắt buộc phải băm (hash) bằng giải thuật **SHA-256** trước khi đưa vào trường `passwordHash` trong gói `LOGIN_REQ`.
2.  **Đóng kết nối an toàn (Graceful Shutdown)**: Khi Client ngắt kết nối một cách chủ động (bấm nút Thoát hoặc đóng ứng dụng), Client phải gửi gói tin `LOGOUT_REQ` (Opcode `4`) để Server xóa Socket khỏi danh sách quản lý kết nối (`activeConnections`) và cập nhật trạng thái ngoại tuyến (`OFFLINE`) tới các client khác trước khi chính thức ngắt kết nối TCP vật lý.
```
