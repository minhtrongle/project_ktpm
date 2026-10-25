# HỆ THỐNG QUẢN LÝ VIỆC ĐẶT BÀN Ở NHÀ HÀNG (NHÀ HÀNG SEN VIỆT)

> **Môn học:** Kiến trúc phần mềm  
> **Kiến trúc áp dụng:** Multi-Tier kết hợp mô hình MVC  
> **Công nghệ nền tảng:** .NET 8 (C# 12) | ASP.NET Core MVC | Entity Framework Core 8 | PostgreSQL 16

---

## MỤC LỤC
1. [Giới thiệu dự án](#1-giới-thiệu-dự-án)
2. [Yêu cầu môi trường & Cài đặt ban đầu](#2-yêu-cầu-môi-trường--cài-đặt-ban-đầu)
3. [Hướng dẫn cài đặt Cơ sở dữ liệu (PostgreSQL)](#3-hướng-dẫn-cài-đặt-cơ-sở-dữ-liệu-postgresql)
4. [Hướng dẫn mở và chạy dự án trên Visual Studio 2022](#4-hướng-dẫn-mở-và-chạy-dự-án-trên-visual-studio-2022)
5. [Danh sách tài khoản đăng nhập mẫu](#5-danh-sách-tài-khoản-đăng-nhập-mẫu)
6. [Hướng dẫn trải nghiệm các tính năng chính](#6-hướng-dẫn-trải-nghiệm-các-tính-năng-chính)
7. [Chạy kiểm thử (Unit / Architecture Tests)](#7-chạy-kiểm-thử-unit--architecture-tests)
8. [Cấu trúc mã nguồn (Multi-Tier)](#8-cấu-trúc-mã-nguồn-multi-tier)
9. [Xử lý sự cố thường gặp (Troubleshooting)](#9-xử-lý-sự-cố-thường-gặp-troubleshooting)

---

## 1. GIỚI THIỆU DỰ ÁN
Dự án được xây dựng nhằm tin học hóa và đồng bộ hóa toàn diện hoạt động vận hành của nhà hàng ẩm thực Sen Việt (quy mô 40 bàn ăn tại 4 phân khu: Tầng trệt, Sân vườn, Tầng lầu, Phòng VIP), phục vụ 5 tác nhân: **Khách hàng**, **Nhân viên phục vụ bàn**, **Đầu bếp/Bar**, **Thu ngân**, và **Quản lý nhà hàng**.

### Các công nghệ sử dụng:
- **Ngôn ngữ & Runtime:** C# 12 trên nền tảng .NET 8 LTS.
- **Tầng Giao diện (Presentation):** ASP.NET Core MVC (Razor Views `.cshtml`), Bootstrap 5.3, JavaScript thuần.
- **Tầng Nghiệp vụ (Business Logic):** Tách biệt độc lập trong `RestaurantManagement.Business`, giao tiếp qua DTOs.
- **Tầng Truy xuất dữ liệu (Data Access):** Entity Framework Core 8, Repository Pattern, Unit of Work, Npgsql Driver.
- **Cơ sở dữ liệu:** PostgreSQL 16 đạt chuẩn 3NF, giao dịch ACID, khóa dòng (Row-level Locking).

---

## 2. YÊU CẦU MÔI TRƯỜNG & CÀI ĐẶT BAN ĐẦU

Để chạy được dự án trên máy tính mới (chưa cài đặt gì trước đó), bạn chỉ cần:

1. **Visual Studio 2022** (Bản Community/Professional/Enterprise phiên bản 17.8 trở lên):
   - Khi cài đặt Visual Studio, tích chọn Workload: **ASP.NET and web development**.
   - Bộ công cụ sẽ tự động kèm theo **.NET 8.0 SDK**.
2. **Dịch vụ Cơ sở dữ liệu PostgreSQL 16**:
   - Dự án sử dụng hệ quản trị CSDL PostgreSQL. Bạn có thể chọn **Cài trực tiếp lên Windows** (Khuyên dùng nếu không quen Docker) hoặc **Dùng Docker Desktop** theo hướng dẫn ở Mục 3 dưới đây.

---

## 3. HƯỚNG DẪN CÀI ĐẶT CƠ SỞ DỮ LIỆU (POSTGRESQL)

Bạn chọn **MỘT TRONG HAI CÁCH** sau:

### CÁCH A: Cài đặt PostgreSQL trực tiếp trên Windows (Đơn giản, không cần Docker)

1. **Tải bộ cài:**  
   Truy cập trang chủ EnterpriseDB: [PostgreSQL Windows Downloads](https://www.enterprisedb.com/downloads/postgres-postgresql-downloads), tải phiên bản **PostgreSQL 16.x** cho Windows (64-bit).
2. **Cài đặt:**  
   - Chạy file `.exe` vừa tải, nhấn **Next**.
   - Ở bước nhập mật khẩu cho tài khoản `postgres`, đặt mật khẩu là: `123`.
   - Ở bước chọn Port, bạn có thể nhập: `4321` (để khớp ngay với cấu hình mặc định) hoặc giữ nguyên cổng mặc định `5432`.
   - Các bước còn lại nhấn **Next** cho đến khi hoàn tất.
3. **Tạo Database:**
   - Mở ứng dụng **pgAdmin 4** (được cài đặt cùng PostgreSQL) từ Start Menu.
   - Nhập mật khẩu `123` để mở kết nối máy chủ.
   - Nhấp chuột phải vào mục **Databases** -> chọn **Create** -> **Database...**
   - Đặt tên Database là: `restaurant_db` -> Nhấn **Save**.

> 💡 **Lưu ý về Port kết nối:**  
> - Nếu bạn cài đặt PostgreSQL với cổng `4321`, dự án sẽ kết nối được ngay.
> - Nếu bạn để cổng mặc định `5432`, hãy mở file `src/RestaurantManagement.Web/appsettings.json`, sửa lại dòng `"Port=4321"` thành `"Port=5432"`.

---

### CÁCH B: Sử dụng Docker Desktop (Dành cho máy đã có hoặc thích dùng Docker)

Nếu máy tính của bạn đã cài **Docker Desktop**:
1. Khởi động ứng dụng Docker Desktop.
2. Mở cửa sổ dòng lệnh (PowerShell / Command Prompt / Git Bash) tại thư mục gốc của dự án (nơi có file `docker-compose.yml`).
3. Chạy câu lệnh sau:
   ```bash
   docker compose up -d
   ```
4. Docker sẽ tự động tải image PostgreSQL 16, tạo database `restaurant_db` và ánh xạ cổng máy chủ sang `4321`. Bạn có thể truy cập pgAdmin quản trị web tại địa chỉ `http://localhost:5050` (Email: `admin@senviet.vn`, Mật khẩu: `123`).

---

## 4. HƯỚNG DẪN MỞ VÀ CHẠY DỰ ÁN TRÊN VISUAL STUDIO 2022

Sau khi hoàn tất cấu hình cơ sở dữ liệu ở Mục 3, bạn thực hiện các bước sau trên Visual Studio:

### Bước 1: Mở Solution
- Khởi động **Visual Studio 2022**.
- Chọn **Open a project or solution**.
- Tìm đến thư mục dự án và chọn tệp `RestaurantManagement.sln`.

### Bước 2: Kiểm tra cấu hình chuỗi kết nối
- Trong cửa sổ **Solution Explorer** (thường ở góc bên phải), mở tệp:  
  `src` -> `RestaurantManagement.Web` -> `appsettings.json`.
- Kiểm tra chuỗi kết nối tại dòng 3:
  ```json
  "ConnectionStrings": {
    "DefaultConnection": "Host=127.0.0.1;Port=4321;Database=restaurant_db;Username=postgres;Password=123"
  }
  ```
  *(Nếu mật khẩu hoặc port PostgreSQL của bạn khác, hãy điều chỉnh lại cho chính xác).*

### Bước 3: Thiết lập Startup Project
- Trong **Solution Explorer**, tìm project `RestaurantManagement.Web`.
- Nhấp **chuột phải** vào `RestaurantManagement.Web` -> chọn **Set as Startup Project** *(Tên project sẽ được in đậm)*.

### Bước 4: Khôi phục gói (Restore) và Biên dịch (Build)
- Nhấp chuột phải vào dòng đầu tiên: **Solution 'RestaurantManagement'** -> chọn **Restore NuGet Packages**.
- Nhấn tổ hợp phím **`Ctrl + Shift + B`** (hoặc vào menu **Build** -> **Build Solution**).
- Đảm bảo ở thanh trạng thái góc dưới bên trái báo: **`Build succeeded (0 errors)`**.

### Bước 5: Khởi tạo Database và Nạp dữ liệu mẫu (TỰ ĐỘNG 100%)
- 🔥 **Bạn KHÔNG cần gõ lệnh migration thủ công!**
- Khi ứng dụng khởi động lần đầu tiên, lớp `DatabaseInitializer` trong mã nguồn sẽ tự động:
  1. Thi hành toàn bộ Migrations để tạo đầy đủ các bảng dữ liệu chuẩn 3NF.
  2. Nạp sẵn dữ liệu mẫu (`DataSeeder`):
     - 4 khu vực (`Tầng trệt`, `Sân vườn`, `Tầng lầu`, `Phòng VIP`).
     - 40 bàn ăn đánh số từ `T01-T15`, `S01-S10`, `L01-L10`, `V01-V05`.
     - 13 món ăn đặc trưng theo các phân khu bếp nóng, bếp lạnh, quầy bar.
     - 4 tài khoản nhân viên tương ứng 4 vai trò.
     - Các trạm máy in nhiệt KOT.

### Bước 6: Chạy ứng dụng
- Trên thanh công cụ Visual Studio, nhấn vào nút Play màu xanh lá **`https`** (hoặc nhấn phím **`F5`** / **`Ctrl + F5`**).
- Trình duyệt sẽ tự động bật lên và hiển thị trang chủ website Nhà hàng Sen Việt tại địa chỉ `https://localhost:7xxx` (hoặc `http://localhost:5xxx`).

---

## 5. DANH SÁCH TÀI KHOẢN ĐĂNG NHẬP MẪU

Hệ thống đã nạp sẵn các tài khoản demo tương ứng với từng vai trò nhân sự:

| Vai trò | Tên tài khoản (Username) | Mật khẩu mặc định | Ghi chú / Quyền hạn |
| :--- | :--- | :--- | :--- |
| **Quản lý (Manager)** | `quanly` | `Admin@123` | Toàn quyền hệ thống: thực đơn, nhân sự, sơ đồ bàn, duyệt hủy/đổi món, báo cáo doanh thu. Mã PIN duyệt: `1234`. |
| **Thu ngân (Cashier)** | `thungan` | `Admin@123` | Mở ca trực đầu ngày, thanh toán hóa đơn, xuất hóa đơn nhiệt, đóng ca kiểm quỹ chốt két tiền. |
| **Phục vụ bàn (Staff)** | `phucvu` | `Admin@123` | Quản lý sơ đồ bàn, xếp khách, ghi nhận gọi món tại bàn, gửi lệnh in báo bếp KOT, in tạm tính. |
| **Đầu bếp (Kitchen)** | `daubep` | `Admin@123` | Màn hình KDS/báo bếp, tiếp nhận đơn, cập nhật trạng thái chế biến (Đang chế biến -> Sẵn sàng). |
| **Khách hàng (Customer)** | *(Không cần đăng nhập)* | *(Không cần)* | Trực tiếp tra cứu bàn trống, tạo yêu cầu đặt bàn, thanh toán cọc và quản lý lịch đặt tại trang chủ. |

---

## 6. HƯỚNG DẪN TRẢI NGHIỆM CÁC TÍNH NĂNG CHÍNH

1. **Khách hàng đặt bàn trực tuyến:**
   - Vào menu **Đặt bàn**, chọn ngày giờ, số khách và khu vực ngồi -> Hệ thống tự động lọc các bàn phù hợp.
   - Nhập thông tin liên hệ, xác nhận cọc ($200.000\text{ đ}$ hoặc $500.000\text{ đ}$) -> Bàn tự động chuyển sang trạng thái đã đặt trước.
2. **Nhân viên phục vụ đón khách & gọi món:**
   - Đăng nhập tài khoản `phucvu` -> Vào màn hình **Sơ đồ bàn**.
   - Bấm vào bàn để **Bắt đầu phục vụ**, chọn món ăn từ thực đơn, thêm ghi chú chế biến và bấm **Gửi báo bếp**.
3. **Khu vực bếp tiếp nhận chế biến:**
   - Đăng nhập tài khoản `daubep` -> Vào màn hình **Bếp & Pha chế**.
   - Xem danh sách món theo trạm (Bếp nóng, Bếp lạnh, Bar) và bấm xác nhận hoàn thành món.
4. **Thu ngân mở ca, thanh toán & đóng ca:**
   - Đăng nhập tài khoản `thungan` -> Thực hiện **Mở ca** đầu ca với định mức tiền lẻ 2.000.000 đ.
   - Khi bàn ăn yêu cầu tính tiền, bấm chọn bàn -> Áp dụng giảm giá thành viên, khấu trừ tiền cọc, tính VAT 8% -> Bấm **Thanh toán**.
   - Cuối ngày, vào chức năng **Đóng ca**, nhập số tiền mặt thực tế kiểm đếm được trong két để đối soát chênh lệch.
5. **Quản lý xem báo cáo tài chính:**
   - Đăng nhập tài khoản `quanly` -> Vào mục **Báo cáo & Thống kê**.
   - Theo dõi doanh thu theo ngày, tỷ lệ tiêu thụ món ăn, phân tích doanh thu thuần và cơ cấu nguồn thu (tiền mặt / chuyển khoản).

---

## 7. CHẠY KIỂM THỬ (UNIT / ARCHITECTURE TESTS)

Dự án bao gồm đầy đủ bộ kiểm thử tự động đạt độ bao phủ cao:
- **Kiểm thử logic nghiệp vụ (Business Tests):** Kiểm tra thuật toán hoàn cọc, tính thuế GTGT 8%, chiết khấu thành viên, phụ thu giờ cao điểm, kiểm soát quỹ két tiền.
- **Kiểm thử quy tắc kiến trúc (Architecture Tests):** Dùng `NetArchTest` để kiểm tra nghiêm ngặt việc không vi phạm phụ thuộc giữa các tầng (Web không được tham chiếu trực tiếp DataAccess, tuân thủ nguyên tắc Multi-Tier).

### Cách chạy trong Visual Studio:
1. Vào menu **Test** -> chọn **Test Explorer** (Phím tắt: `Ctrl + E, T`).
2. Nhấn nút **Run All Tests In View** (biểu tượng hai mũi tên xanh Play).
3. Kết quả: Toàn bộ **18/18 tests** đều chạy thành công (`Passed`).

*(Hoặc mở Terminal gõ lệnh: `dotnet test`)*.

---

## 8. CẤU TRÚC MÃ NGUỒN (MULTI-TIER)

Dự án được phân chia thành các project vật lý độc lập nhằm đảm bảo tính đơn nhiệm và dễ bảo trì:

```text
ProjectRestaurant/
├── src/
│   ├── RestaurantManagement.Web/            # Presentation Layer: ASP.NET Core MVC, Razor Views, Controllers, ViewModels
│   ├── RestaurantManagement.Business/       # Business Logic Layer (BLL): Services, DTOs, Quy tắc nghiệp vụ, Thuật toán tính toán
│   ├── RestaurantManagement.DataAccess/     # Data Access Layer (DAL): EF Core DbContext, Entities, Repositories, Migrations, Seed
│   └── RestaurantManagement.Common/         # Thư viện dùng chung: Enums hệ thống, Hằng số quy định, Exception models
├── tests/
│   ├── RestaurantManagement.Business.Tests/      # Kiểm thử đơn vị các nghiệp vụ tính tiền, cọc, giờ cao điểm
│   ├── RestaurantManagement.ArchitectureTests/  # Kiểm thử kiến trúc đa tầng (NetArchTest)
│   └── RestaurantManagement.IntegrationTests/   # Kiểm thử tích hợp khởi tạo CSDL
├── docs/                                    # Báo cáo kiến trúc phần mềm đầy đủ (phantichtongquan.md), tài liệu ADRs
├── docker-compose.yml                       # File dựng nhanh PostgreSQL 16 và pgAdmin qua Docker
└── RestaurantManagement.sln                 # File giải pháp Visual Studio
```

---

## 9. XỬ LÝ SỰ CỐ THƯỜNG GẶP (TROUBLESHOOTING)

### 1. Lỗi kết nối CSDL: `Npgsql.NpgsqlException: Connection refused` hoặc `SocketException`
- **Nguyên nhân:** Dịch vụ PostgreSQL chưa được bật hoặc cổng kết nối (Port) không trùng khớp.
- **Cách khắc phục:**
  - Kiểm tra xem dịch vụ PostgreSQL (hoặc container Docker) đã đang chạy hay chưa.
  - Mở file `src/RestaurantManagement.Web/appsettings.json`, kiểm tra xem cổng `Port=...` có đúng với cổng PostgreSQL của bạn (4321 hay 5432) không.

### 2. Cảnh báo chứng chỉ bảo mật HTTPS trên trình duyệt
- **Nguyên nhân:** Máy tính chưa tin cậy chứng chỉ phát triển cục bộ của ASP.NET Core.
- **Cách khắc phục:** Mở Command Prompt hoặc PowerShell và chạy lệnh sau, sau đó chọn **Yes**:
  ```bash
  dotnet dev-certs https --trust
  ```

### 3. Lỗi `Database 'restaurant_db' does not exist`
- **Nguyên nhân:** Chưa tạo database tên `restaurant_db` trước khi chạy.
- **Cách khắc phục:** Mở pgAdmin 4, chuột phải vào **Databases** -> **Create Database** với tên `restaurant_db`. Sau đó chạy lại ứng dụng để hệ thống tự động sinh bảng và nạp dữ liệu.

---
*Chúc bạn có trải nghiệm tốt với Hệ thống Quản lý Nhà hàng Sen Việt! Mọi thắc mắc vui lòng liên hệ nhóm phát triển.*
