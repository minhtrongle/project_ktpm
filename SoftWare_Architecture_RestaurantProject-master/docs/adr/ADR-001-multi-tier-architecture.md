# ADR-001: Áp dụng kiến trúc Multi-Tier phân tách nghiêm ngặt kết hợp MVC

## Trạng thái
Đã chấp thuận (Accepted) - 2026-10-14

## Bối cảnh
Dự án "Hệ thống quản lý việc đặt bàn và vận hành Nhà hàng Sen Việt" yêu cầu phân tách rõ ràng trách nhiệm giữa các tầng (Presentation, Business, Data Access) nhằm đạt tính mở rộng, bảo trì và kiểm thử cao. Trước đây, toàn bộ logic bị trộn lẫn trong một project Web duy nhất và ranh giới phân tầng chưa được ràng buộc chặt chẽ.

## Quyết định
1. Xây dựng Solution gồm 4 project chính:
   - `RestaurantManagement.Web`: Tầng Presentation sử dụng ASP.NET Core MVC (chia Areas: Public, Pos, Admin).
   - `RestaurantManagement.Business`: Tầng Business chứa toàn bộ logic, quy tắc (Policies), DTOs và Interfaces.
   - `RestaurantManagement.DataAccess`: Tầng Data Access chịu trách nhiệm giao tiếp CSDL qua EF Core, Repositories, Unit of Work và External Gateways.
   - `RestaurantManagement.Common`: Shared Kernel chứa Enums, Constants, Results (`Result<T>`), không phụ thuộc tầng nào.
2. Bật cờ `<DisableTransitiveProjectReferences>true</DisableTransitiveProjectReferences>` trong `RestaurantManagement.Web.csproj` để Web không thể gọi trực tiếp sang `DataAccess`.
3. Bổ sung `ArchitectureTests` sử dụng `NetArchTest.Rules` để tự động kiểm tra ranh giới phụ thuộc khi build và chạy CI.

## Hệ quả
- Tích cực: Phân định ranh giới tuyệt đối; Presentation không thể bypass Business để gọi DB; kiểm thử Unit Test dễ dàng không phụ thuộc Web hay DB.
- Hạn chế: Cần viết thêm DTOs và mapping qua lại giữa ViewModel ↔ DTO ↔ Entity.
