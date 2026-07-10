# Database schema audit

Đối chiếu cập nhật ngày 11/07/2026 giữa `databasenew (1).sql`, EF Core models và các nơi truy cập `_context` trong code.

## Đã bổ sung

- `HotelBookings`: thêm mốc dự kiến/thực tế (`ScheduledCheckInDate`, `ScheduledCheckOutDate`, `ActualCheckInAt`, `ActualCheckOutAt`) để không mất lịch sử khi check-in/check-out cập nhật thời gian.
- `PetBioTimelines`: thêm `HotelBookingId`, foreign key và index để mỗi hoạt động thuộc đúng một lần lưu trú.
- `MedicalRecords`: thêm `HotelBookingId`, foreign key và index để hồ sơ y tế phát sinh trong thời gian lưu trú xuất hiện đúng booking.
- `FoodDiaryLogs`: thêm `HotelBookingId`, `OccurredAt`, foreign key và index để nhật ký chăm sóc không còn phải ghép bằng tên pet/chuồng.
- `FoodDiaryLogs`: bổ sung `ActivityType`, `Title`, `MediaUrl`, `MediaType`, `IsVisibleToCustomer`, `CreatedByUserId` cho Daily Care Log có ảnh/video và quyền hiển thị.
- `CustomerNotifications`: bảng mới lưu thông báo bền vững, trạng thái đã đọc và liên kết đúng customer/booking; SignalR chỉ đảm nhiệm đẩy realtime.
- `SpaReviews`: code đã đọc/ghi bảng này nhưng file SQL được cung cấp chưa tạo bảng.
- `SpaServices.TargetSpecies`: code đang sử dụng cột này nhưng file SQL được cung cấp chưa có cột.

Script riêng: `20260710_HotelBookingHistoryAndSchemaSync.sql` và `20260711_DailyCareLogsAndCustomerNotifications.sql`.
Để cập nhật thủ công cả hai chức năng trong một lần, dùng `20260711_HotelHistoryAndDailyCare_Manual.sql`.

## Chênh lệch còn lại

- `CreatedByUserId` hiện lưu dấu vết người tạo nhưng chưa đặt foreign key tới `Users`, nhằm giữ nguyên nhật ký nếu tài khoản nhân viên bị xóa. Nếu hệ thống chuyển sang khóa mềm hoàn toàn, có thể bổ sung quan hệ này.
- Camera trực tiếp chưa cần thêm cột stream URL vào database. URL RTSP/HLS chứa thông tin truy cập không nên đặt trong `Cages`; giai đoạn camera cần bảng cấu hình riêng và phát token xem ngắn hạn theo booking.
- Project còn `Room`, `RoomRepository`, `RoomService`, nhưng luồng Hotel thực tế dùng `RoomTypes` và `Cages`; không có bảng `Rooms` trong file SQL. Các lớp `Room*` hiện là code legacy chưa được controller sử dụng.
- Project không có EF migrations hoặc test project. Schema trước đây được nối bằng nhiều đoạn `ALTER TABLE` trong file SQL và startup, nên khó kiểm soát phiên bản database giữa các máy.

## Trạng thái kiểm tra database thật

Build đã thành công. Ngày 11/07/2026, ứng dụng chạy ngoài sandbox trả HTTP 200 và truy vấn metadata trực tiếp trên SQL Server `DESKTOP-LH1AVTF` đã xác nhận các cột Daily Care mới cùng bảng `CustomerNotifications` tồn tại. Việc kiểm thử thao tác bằng tài khoản staff/customer vẫn cần thực hiện thủ công với dữ liệu đăng nhập của dự án.
