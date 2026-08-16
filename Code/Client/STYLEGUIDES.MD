# Style Guide – ChatTCP (Giai đoạn 1 – Dựng khung UI tĩnh)

> Bản rút gọn dùng cho Giai đoạn 1: dựng `ChatView.xaml` / `ContactListView.xaml` với dữ liệu giả, chưa nối Server. Chỉ giữ lại phần cần thiết để có giao diện nhìn ổn ngay từ đầu — Dark Mode, trạng thái nâng cao sẽ bổ sung ở Giai đoạn 3-4.

---

## 1. Bảng màu (chỉ Light Mode)

| Tên biến | Mã màu (Hex) | Dùng cho |
|---|---|---|
| `PrimaryColor` | `#2563EB` | Nút Gửi, bong bóng chat của mình |
| `SecondaryBubbleColor` | `#F3F4F6` | Bong bóng chat của người khác |
| `BackgroundColor` | `#FFFFFF` | Nền chính toàn app |
| `SidebarBackgroundColor` | `#F9FAFB` | Nền sidebar danh sách liên hệ |
| `BorderColor` | `#E5E7EB` | Viền phân cách sidebar / khung chat |
| `TextPrimaryColor` | `#111827` | Nội dung tin nhắn, tên liên hệ |
| `TextSecondaryColor` | `#6B7280` | Thời gian, tin nhắn preview |
| `OnlineStatusColor` | `#22C55E` | Chấm báo online (chỉ cần style sẵn, logic thật làm sau) |

> Dark Mode, `ErrorColor`, `ForwardedLabelColor` **để dành cho Giai đoạn 3** khi làm Reply/Forward/Error handling thật — chưa cần khai báo bây giờ.

---

## 2. Typography – Roboto (rút gọn)

### Cài đặt (làm 1 lần)
1. Tải Roboto Regular + Medium + Bold từ Google Fonts
2. Đặt vào `ChatTCP.Client/Assets/Fonts/`
3. Set **Build Action = Resource**
4. Khai báo trong `App.xaml`:
```xml
<FontFamily x:Key="AppFontFamily">pack://application:,,,/Assets/Fonts/#Roboto</FontFamily>
```

### Cấp bậc chữ cần dùng ngay

| Tên biến | Cỡ chữ | Weight | Dùng cho |
|---|---|---|---|
| `HeadingMedium` | 16px | Medium (500) | Tên liên hệ trong sidebar, tên cuộc trò chuyện |
| `BodyRegular` | 14px | Regular (400) | Nội dung tin nhắn |
| `Caption` | 12px | Regular (400) | Thời gian, tin nhắn preview |

> Các cấp `HeadingLarge`, `CaptionSmall`, `ButtonText` bổ sung khi cần đến (nhãn forward, tiêu đề lớn...) ở giai đoạn sau.

---

## 3. Kích thước cơ bản

### Avatar
| Vị trí | Kích thước |
|---|---|
| Sidebar danh sách liên hệ | 40×40px, bo tròn 50% |
| Khung chat (cạnh bong bóng) | 32×32px, bo tròn 50% |

### Bong bóng chat
| Thuộc tính | Giá trị |
|---|---|
| Bo góc | 12px |
| Padding | 12px ngang, 8px dọc |
| Độ rộng tối đa | 65% chiều rộng khung chat |

### Layout tổng thể (2 cột)
| Khu vực | Kích thước |
|---|---|
| Sidebar | Cố định 280px |
| Khung chat | Chiếm phần còn lại |
| Thanh nhập tin nhắn | Cao tối thiểu 48px |

