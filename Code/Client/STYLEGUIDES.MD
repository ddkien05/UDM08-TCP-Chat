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

---

## 4. Resource tokens (dùng trong XAML)

Những resource chính đã định nghĩa trong `Assets/Theme/Styles.xaml` và cần được dùng xuyên suốt:

- Brushes:
  - `PrimaryBrush` (#2563EB)  dùng cho nút chính (Send), bong bóng của mình.
  - `SecondaryBubbleBrush` (#F3F4F6)  dùng cho bong bóng của người khác.
  - `BackgroundBrush`, `SidebarBackgroundBrush`, `BorderColor`, `TextColor_Primary`, `TextColor_Secondary`, `TextOnPrimaryBrush`.

- Size tokens:
  - `SidebarWidth` (280)
  - `AvatarSize_Sidebar` (40), `AvatarSize_Chat` (32)
  - `BubbleCornerRadius` (12), `BubblePadding` (12,8,12,8)
  - `ChatInputHeight` (48)

Sử dụng tên resource thay vì mã màu / số cố định giúp đảm bảo nhất quán khi chỉnh sửa sau này.

---

## 5. Component styles (mô tả nhanh)

- ChatBubbleSelf (Border)
  - Background: `PrimaryBrush`, Foreground: `TextOnPrimaryBrush`.
  - CornerRadius: `BubbleCornerRadius`, Padding: `BubblePadding`.
  - Margin nhỏ bên trái để phân tách với biên khung.

- ChatBubbleOther (Border)
  - Background: `SecondaryBubbleBrush`, Foreground: `TextColor_Primary`.
  - Giữ padding/CornerRadius giống nhau để đồng bộ.

- ChatAvatar_Sidebar / ChatAvatar_Chat (Image)
  - Kích thước theo token; dùng `UniformToFill` để đảm bảo ảnh không méo.

- ChatInputBox (TextBox)
  - Cao cố định `ChatInputHeight`, padding 10, border 1px `BorderColor`.

- ChatSendButton (Button)
  - Nền `PrimaryBrush`, chữ trắng, bo góc nhỏ.

- ContactListItem (ListBoxItem)
  - Hover: nền `SidebarBackgroundBrush`.
  - Selected: nền `SecondaryBubbleBrush`.

---

## 6. UX rules & notes (dùng trong báo cáo đồ án)

- Tin nhắn dài: hiển thị wrap theo `MaxWidth` (tối đa ~65% khung chat). Không cắt nội dung bằng dấu `...` trừ khi cần preview trong sidebar.
- Thời gian (timestamp): hiển thị bằng `Caption` style, đặt cạnh hoặc dưới bong bóng, căn phải cho message của mình.
- Placeholder trong input: "Type a message..." (dùng TextBox watermark nếu cần) — giữ văn bản mờ, không dùng animation.
- Reply/Forward preview: hiện một thanh nhỏ phía trên composer, hiển thị tên người gửi + snippet (max 2 dòng) và nút hủy.
- Accessibility: đảm bảo contrast tối thiểu (PrimaryBrush trên nền trắng tương phản tốt). Tránh dùng font size < 12px.

---

## 7. Hướng dẫn nhanh tích hợp vào XAML (ví dụ dùng ngay)

1. Reference Styles.xaml trong `App.xaml` (đã có sẵn trong dự án):
```xml
<ResourceDictionary Source="Assets/Theme/Styles.xaml"/>
```

2. Dùng style trong DataTemplate / control:
```xml
<Border Style="{StaticResource ChatBubbleOther}">
  <TextBlock Text="Hello" Style="{StaticResource BodyRegular}"/>
</Border>

<Button Style="{StaticResource ChatSendButton}" Content="Gửi" />
```

3. Contact list:
```xml
<ListBox ItemContainerStyle="{StaticResource ContactListItem}">
  <!-- item template with avatar + name + preview -->
</ListBox>
```

---

## 8. Ghi chú cho lần nộp đồ án

- Trong báo cáo, đính kèm `Styles.xaml` (hoặc paste phần tokens + styles) để giảng viên thấy cấu trúc thiết kế.
- Trình bày một mục ngắn giải thích vì sao chọn palette nhẹ, Roboto, và spacing vừa phải (để tập trung vào kết nối TCP & protocol hơn là UI fancy).

---

_Document prepared for ChatTCP – academic project (Phase 1)._


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

