# ADR-002: Áp dụng EF Core kết hợp Repository Pattern và Unit of Work với PostgreSQL snake_case

## Trạng thái
Đã chấp thuận (Accepted) - 2026-10-14

## Bối cảnh
PostgreSQL theo quy ước hệ sinh thái ưa chuộng `snake_case` cho tên bảng và cột. Cần quản lý vòng đời DbContext và các giao dịch (Transactions) đồng thời giữa nhiều repository để đảm bảo tính toàn vẹn ACID.

## Quyết định
1. Sử dụng EF Core với `EFCore.NamingConventions` (`UseSnakeCaseNamingConvention()`) để tự động chuyển đổi tên bảng và cột sang snake_case.
2. Áp dụng generic `IRepository<T>` kết hợp các repository chuyên biệt (`ITableRepository`, `IBookingRepository`, v.v.).
3. Triển khai `IUnitOfWork` chịu trách nhiệm `SaveChangesAsync()` và quản lý giao dịch (`ExecuteInTransactionAsync()`). Không cho phép repository tự ý commit giao dịch.
4. Tách biệt cấu hình Entity ra các file `IEntityTypeConfiguration<T>` độc lập trong thư mục `Configurations/`.

## Hệ quả
- Tích cực: SQL sinh ra đồng nhất, dễ theo dõi trên PostgreSQL (không cần trích dẫn `""`), giao dịch nghiệp vụ an toàn.
- Hạn chế: Thêm lớp trừu tượng Unit of Work và Repository cần cấu hình Dependency Injection chuẩn mực.
