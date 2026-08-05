
# Style Guide

Màu sắc, phông chữ, kích thước, kiểu chữ sử dụng chung cho toàn project

## Bảng Màu
**1.2 Light mode**

| Tên biến | Mã Màu | Mục đích sử dụng|
|----------|--------|-----------------|
|PrimaryColor| #2563EB | Nút chính, bong bóng chat, link, icon active|
|SecondaryBubbleColor| #F3F4F6 | bong bóng chat của người khác|
|BackgroundColor| #FFFFFF | Nền chính toàn app|
|SidebarBackgroundColor| #F9FAFB | Nền sidebar danh sách liên hệ|
|BorderColor| #E5E7EB | viền ngăn sidebar và khung chat|
|TextPrimaryColor| #111827| Nội dung tin nhắn, tên liên hệ|
|TextSecondaryColor| #6B7280 | Thời gian, tin nhắn preview, trạng thái|
|OnlineStatusColor| #22C55E | Chấm thông báo online|

## Typography 
**1. Font Roboto**

Cài đặt (làm 1 lần)

	1. Tải Roboto Regular + Medium + Bold từ Google Fonts
	2. Đặt vào ChatTCP.Client/Assets/Fonts/
	3. Set Build Action = Resource
	4. Khai báo trong App.xaml

**2. Cấp bậc chữ**

|Tên biến|Cỡ chữ|Độ đậm|Mục đích sử dụng|
|--------|------|------|----------------|
|HeadingMedium|16px|Medium(500)| Tên liên hệ trong sidebar|
|BodyRegular|14px|Regular(400)|Nội dung tin nhắn chính|
|Caption|12px|Regular(400)|Thời gian, tin nhắn preview|

**3. Kích thước**

**Avatar**

|Vị trí|Kích thước|
|------|----------|
|Sidebar danh sách liên hệ|40x40px, bo tròn 50%|
|Khung chat (cạnh bong bóng)| 32x32px, bo tròn 50%|

**Bong bóng chat**

|Thuộc tính| Giá trị|
|----------|--------|
|Bo góc|12px|
|Padding|12px ngang, 8px dọc|
|Độ rộng tối đa|65% chiều rộng khung chat|

**Layout tổng thể**

|Khu vực| Kích thước|
|-------|-----------|
|Sidebar|Cố định 280px|
|Khung chat|Chiếm phần còn lại|
|Thanh nhập tin nhắn| Cao tối thiểu 48px|