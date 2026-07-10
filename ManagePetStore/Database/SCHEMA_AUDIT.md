# Database schema audit

Đối chiếu ngày 10/07/2026 giữa `databasenew (1).sql`, EF Core models và các nơi truy cập `_context` trong code.

## Đã bổ sung

- `HotelBookings`: thêm mốc dự kiến/thực tế (`ScheduledCheckInDate`, `ScheduledCheckOutDate`, `ActualCheckInAt`, `ActualCheckOutAt`) để không mất lịch sử khi check-in/check-out cập nhật thời gian.
- `PetBioTimelines`: thêm `HotelBookingId`, foreign key và index để mỗi hoạt động thuộc đúng một lần lưu trú.
- `MedicalRecords`: thêm `HotelBookingId`, foreign key và index để hồ sơ y tế phát sinh trong thời gian lưu trú xuất hiện đúng booking.
- `FoodDiaryLogs`: thêm `HotelBookingId`, `OccurredAt`, foreign key và index để nhật ký chăm sóc không còn phải ghép bằng tên pet/chuồng.
- `SpaReviews`: code đã đọc/ghi bảng này nhưng file SQL được cung cấp chưa tạo bảng.
- `SpaServices.TargetSpecies`: code đang sử dụng cột này nhưng file SQL được cung cấp chưa có cột.

Script đồng bộ idempotent: `20260710_HotelBookingHistoryAndSchemaSync.sql`.

## Chênh lệch còn lại

- File SQL có `Wallets`, `WalletTransactions`, `ReturnRequests`, `ReturnRequestItems`, nhưng project chưa có EF models/controllers cho các phân hệ này. Đây là schema đi trước chức năng, không phải lỗi thiếu bảng của code đang chạy.
- Project còn `Room`, `RoomRepository`, `RoomService`, nhưng luồng Hotel thực tế dùng `RoomTypes` và `Cages`; không có bảng `Rooms` trong file SQL. Các lớp `Room*` hiện là code legacy chưa được controller sử dụng.
- Project không có EF migrations hoặc test project. Schema trước đây được nối bằng nhiều đoạn `ALTER TABLE` trong file SQL và startup, nên khó kiểm soát phiên bản database giữa các máy.

## Trạng thái kiểm tra database thật

Build đã thành công, nhưng phiên chạy trong sandbox không kết nối được SQL Server `DESKTOP-LH1AVTF`. Vì quyền chạy ngoài sandbox không được cấp, audit này xác nhận schema bằng source/file SQL; chưa xác nhận trực tiếp metadata và dữ liệu của instance SQL Server trên máy.
