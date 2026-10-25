# Quy định Kiến trúc Nhà hàng Sen Việt (MVC Multi-Tier)

> File này là luật bất biến cho toàn bộ mã nguồn của dự án và mọi phiên lập trình (kể cả AI và con người).

## 1. Cấu trúc Solution và Ranh giới Phân tầng

```
src/
├── RestaurantManagement.Common        (Shared Kernel: Enums, Constants, Result<T>)
├── RestaurantManagement.DataAccess    (Entities, DbContext, Migrations, Repositories, UnitOfWork, External Gateways)
├── RestaurantManagement.Business      (Services, Policies, DTOs, Mapping, Validators)
└── RestaurantManagement.Web           (Presentation: Areas, Controllers, Razor Views, ViewModels, SignalR Hubs, Jobs)
```

### Bảng Quy Tắc Phụ Thuộc (Được kiểm tra tự động bởi NetArchTest)

| Tầng | Được Phép Tham Chiếu | TUYỆT ĐỐI BỊ CẤM |
| :--- | :--- | :--- |
| **RestaurantManagement.Web** | `Business`, `Common` | `DataAccess`, `Microsoft.EntityFrameworkCore`, Entities trực tiếp |
| **RestaurantManagement.Business** | `DataAccess` (qua Interface), `Common` | `Web`, `Microsoft.AspNetCore.Mvc`, `HttpContext`, Razor |
| **RestaurantManagement.DataAccess** | `Common`, `EF Core`, `Npgsql` | `Business`, `Web` |
| **RestaurantManagement.Common** | Không tham chiếu tầng nào | Mọi project khác |

---

## 2. Quy ước Code theo Từng Tầng

### 2.1. Presentation Layer (RestaurantManagement.Web)
- **Controllers**:
  - Nhận `ViewModel` → Validate `ModelState` → Chuyển sang DTO → Gọi **duy nhất 1 Service** → Nhận `Result<T>` → Map sang `ViewModel` → Trả lời qua `View()` hoặc `RedirectToAction()` (mẫu PRG - Post-Redirect-Get).
  - Gắn `[Authorize(Policy = ...)]` theo đúng phân quyền.
  - Form POST bắt buộc có `[ValidateAntiForgeryToken]`.
  - **CẤM**: Truy vấn LINQ, khởi tạo `DbContext`, tính toán tiền nong / phụ thu / chiết khấu trực tiếp trong Controller.
- **ViewModels**:
  - Chỉ chứa dữ liệu hiển thị, chuỗi định dạng (tiền tệ, ngày tháng), danh sách chọn (`SelectList`).
  - Sử dụng DataAnnotations để xác thực dữ liệu đầu vào phía Client/Web.
  - **CẤM**: Sử dụng Entity trực tiếp trong ViewModel hoặc View.

### 2.2. Business Layer (RestaurantManagement.Business)
- **Services**:
  - Tiếp nhận DTO, kiểm tra nghiệp vụ tầng 2 (bắt buộc).
  - Điều phối Repository thông qua `IUnitOfWork`.
  - Mở transaction khi cần bảo đảm tính ACID (đặc biệt khi lập hóa đơn, đặt bàn, hủy món).
  - Trả về `Result` hoặc `Result<T>`, không ném ngoại lệ nghiệp vụ ra ngoài.
  - **CẤM**: Tham chiếu `HttpContext`, `ModelState`, `IActionResult`.
- **Policies**:
  - Các hàm thuần túy (pure functions), không I/O (không gọi CSDL, không gọi mạng).
  - Ví dụ: `BillingCalculator`, `RefundPolicy`, `DepositPolicy`, `PeakHourPolicy`, `LatePolicy`.
  - Bắt buộc phải có Unit Test bao phủ 100%.

### 2.3. Data Access Layer (RestaurantManagement.DataAccess)
- **Repositories**:
  - Chỉ thực hiện truy vấn dữ liệu (LINQ, `AsNoTracking` khi chỉ đọc, projection).
  - Khi cần khóa bi quan: sử dụng `FOR UPDATE` (`FromSqlInterpolated`).
  - Không gọi `SaveChanges` trực tiếp trong Repository; việc commit thuộc trách nhiệm của `IUnitOfWork`.
- **Entities**:
  - Khai báo thực thể CSDL với Fluent API trong `Configurations/`.
  - Sử dụng chuẩn đặt tên `snake_case` cho CSDL PostgreSQL.

### 2.4. Common Layer (RestaurantManagement.Common)
- Chứa các `Enums` (`TableStatus`, `BookingStatus`, `OrderItemStatus`, `StaffRole`, v.v.).
- Chứa các hằng số: `Roles`, `Policies`.
- Chứa cấu trúc kết quả thao tác: `Result`, `Result<T>`, `Error`.
