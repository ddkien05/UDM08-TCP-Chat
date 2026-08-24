## 1. Cấu trúc Khung dữ liệu (Frame Format)

**Định nghĩa Message Framing:**
Quy định cấu trúc phân định ranh giới cho từng gói tin riêng biệt khi truyền qua luồng dữ liệu liên tục (Byte Stream) của TCP giúp ứng dụng xác định chính xác điểm bắt đầu/kết thúc của mỗi thông điệp, giải quyết triệt để hiện tượng Dính gói và Xé gói.

Vì TCP truyền theo dạng byte stream, gói tin ứng dụng sẽ được đóng gói theo dạng Length-Prefixed Frame để chống dính/xé gói tin:

| Thành phần | Kích thước | Mô tả |
| :--- | :--- | :--- |
| **Length** | 4 Bytes | Độ dài của Payload (Số nguyên 32-bit Big-Endian) |
| **Payload** | Biến thiên | Chuỗi JSON chứa nội dung tin nhắn (Mã hóa UTF-8) |

* Length (4 Bytes): Số nguyên 32-bit (Big-Endian) thể hiện độ dài theo Byte của Payload.
* Payload: Chuỗi JSON chứa thông tin lệnh, mã hóa dạng UTF-8 (để hiển thị Tiếng Việt và Emoji).

---

## 2. Định dạng chung Payload (JSON)

Tất cả gói tin gửi qua TCP đều tuân theo cấu trúc JSON gốc:

```json
{
  "type": "TÊN_LOẠI_GÓI_TIN",
  "seq": 1001,
  "timestamp": 1718000000,
  "data": { ... }
}
```

---

## 3. Danh sách Mã gói tin (Packet Types)

**Định nghĩa Message Type:**
Giao thức phải chỉ định có những loại thông điệp nào có thể được gửi và nhận. Hai loại chính là Request message và Response message.

| Mã Type | Chiều truyền | Mô tả chức năng |
| :--- | :--- | :--- |
| AUTH_REQ | Client ➔ Server | Yêu cầu kết nối / đăng nhập |
| AUTH_RES | Server ➔ Client | Phản hồi kết quả đăng nhập |
| CHAT_MSG | Client ⇄ Server | Gửi / Nhận tin nhắn (Reply, Forward) |
| USER_STATUS_NOTIFY | Server ➔ Client | Cập nhật trạng thái Online/Offline của người dùng |
| HEARTBEAT_PING | Client ➔ Server | Gói tin duy trì kết nối |
| HEARTBEAT_PONG | Server ➔ Client | Phản hồi duy trì kết nối |
| ERROR | Server ➔ Client | Thông báo lỗi |

---

## 4. Chi tiết Định dạng các Gói tin

### 4.1. Xác thực & Kết nối (AUTH)

Client gửi yêu cầu đăng nhập (AUTH_REQ):
```json
{
  "type": "AUTH_REQ",
  "seq": 1,
  "timestamp": 1718000001,
  "data": {
    "username": "nguyenvana",
    "display_name": "Nguyễn Văn A",
    "avatar_url": "https://cdn.example.com/avatars/user_a.png"
  }
}
```

Server phản hồi (AUTH_RES):
```json
{
  "type": "AUTH_RES",
  "seq": 1,
  "timestamp": 1718000002,
  "data": {
    "code": 200,
    "message": "Success",
    "user_id": "usr_101"
  }
}
```

---

### 4.2. Gửi & Nhận Tin nhắn (CHAT_MSG)
(Hỗ trợ: Reply, Forward, Emoji, Avatar)

```json
{
  "type": "CHAT_MSG",
  "seq": 102,
  "timestamp": 1718000010,
  "data": {
    "msg_id": "msg_889911",
    "target_type": "PRIVATE", 
    "target_id": "usr_102",
    "sender": {
      "user_id": "usr_101",
      "display_name": "Nguyễn Văn A",
      "avatar_url": "https://cdn.example.com/avatars/user_a.png"
    },
    "content": "Chào bạn! Hôm nay họp lúc mấy giờ nhỉ? 😊🔥",
    "reply_to": {
      "msg_id": "msg_889900",
      "sender_name": "Trần Thị B",
      "content_snippet": "Trưa nay ăn gì nhỉ?"
    },
    "is_forwarded": false,
    "forward_from_name": null
  }
}
```

---

### 4.3. Cập nhật Trạng thái Online / Offline (USER_STATUS_NOTIFY)

```json
{
  "type": "USER_STATUS_NOTIFY",
  "timestamp": 1718000050,
  "data": {
    "user_id": "usr_102",
    "display_name": "Trần Thị B",
    "status": "ONLINE", 
    "last_seen": 1718000050
  }
}
```

---

### 4.4. Giữ kết nối (Heartbeat / Keep-Alive)

```json
{ "type": "HEARTBEAT_PING", "timestamp": 1718000100 }
```

```json
{ "type": "HEARTBEAT_PONG", "timestamp": 1718000100 }
```

---

## 5. Bảng Mã Lỗi (Error Codes)

| Mã lỗi | Tên lỗi | Mô tả |
| :--- | :--- | :--- |
| 200 | SUCCESS | Thao tác thành công |
| 400 | BAD_REQUEST | Cấu trúc gói tin JSON không hợp lệ |
| 401 | UNAUTHORIZED | Chưa đăng nhập / Chưa xác thực |
| 404 | NOT_FOUND | Người nhận hoặc Phòng chat không tồn tại/Offline |
| 500 | INTERNAL_ERROR | Lỗi xử lý Socket hoặc dữ liệu tại Server |