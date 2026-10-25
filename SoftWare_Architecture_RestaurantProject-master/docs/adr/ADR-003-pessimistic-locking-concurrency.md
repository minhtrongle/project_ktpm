# ADR-003: Kiểm soát tương tranh trong Giữ bàn và Đặt bàn bằng Khóa bi quan và Exclusion Constraint

## Trạng thái
Đã chấp thuận (Accepted) - 2026-10-14

## Bối cảnh
Trong môi trường nhà hàng vào giờ cao điểm, nhiều khách hàng hoặc nhân viên có thể đồng thời thao tác giữ hoặc đặt cùng một bàn trong cùng một khung giờ. Nếu chỉ kiểm tra bằng truy vấn `SELECT` thông thường trước khi `INSERT`, hiện tượng Race Condition (Overbooking) sẽ xảy ra.

## Quyết định
1. Sử dụng cơ chế phòng thủ 2 lớp:
   - **Lớp 1 (Ứng dụng)**: Khóa bi quan ở mức bản ghi (Row-level pessimistic locking) bằng cú pháp `SELECT ... FOR UPDATE` trên dòng bàn tương ứng trong suốt transaction giữ bàn.
   - **Lớp 2 (Cơ sở dữ liệu)**: Bổ sung ràng buộc loại trừ PostgreSQL (Exclusion Constraint):
     `EXCLUDE USING gist (table_id WITH =, tstzrange(start_time, end_time, '[)') WITH &&) WHERE (status IN ('PendingPayment', 'Confirmed'))`
     nhờ extension `btree_gist`.
2. Bổ sung token kiểm soát tương tranh lạc quan (`xmin` hoặc Version) cho các thực thể thường xuyên sửa đổi trạng thái như `DiningTable`, `Order`.

## Hệ quả
- Tích cực: Loại bỏ hoàn toàn khả năng đặt trùng bàn (double-booking) ngay cả khi có hàng chục request đồng thời.
- Hạn chế: Khóa dòng giữ lâu hơn một chút trong transaction, cần giới hạn thời gian transaction ngắn và có timeout thích hợp.
