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
