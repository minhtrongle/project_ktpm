using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RestaurantManagement.Common.Enums;
using RestaurantManagement.DataAccess.Context;
using RestaurantManagement.DataAccess.Entities;

namespace RestaurantManagement.DataAccess.Seed;

public static class DataSeeder
{
    public static async Task SeedAsync(RestaurantDbContext context)
    {
        // 1. Seed Areas and Tables (40 tables across 4 areas)
        if (!await context.Areas.AnyAsync())
        {
            var areaGround = new Area { AreaName = "Tầng trệt", Description = "Khu vực sảnh chính trong nhà", DisplayOrder = 1 };
            var areaGarden = new Area { AreaName = "Sân vườn", Description = "Không gian mở thoáng đãng", DisplayOrder = 2 };
            var areaFloor = new Area { AreaName = "Tầng lầu", Description = "Không gian ấm cúng trên lầu", DisplayOrder = 3 };
            var areaVip = new Area { AreaName = "Phòng VIP", Description = "Phòng tiệc riêng tư sang trọng", DisplayOrder = 4 };

            context.Areas.AddRange(areaGround, areaGarden, areaFloor, areaVip);
            await context.SaveChangesAsync();

            var tables = new List<DiningTable>();

            // Tầng trệt: T01 - T15
            for (int i = 1; i <= 15; i++)
            {
                var cap = (i % 3 == 0) ? 8 : (i % 2 == 0 ? 4 : 2);
                tables.Add(new DiningTable
                {
                    TableNumber = $"T{i:D2}",
                    AreaId = areaGround.Id,
                    Capacity = cap,
                    Status = TableStatus.Available
                });
            }

            // Sân vườn: S01 - S10
            for (int i = 1; i <= 10; i++)
            {
                var cap = (i % 2 == 0) ? 6 : 4;
                tables.Add(new DiningTable
                {
                    TableNumber = $"S{i:D2}",
                    AreaId = areaGarden.Id,
                    Capacity = cap,
                    Status = TableStatus.Available
                });
            }

            // Tầng lầu: L01 - L10
            for (int i = 1; i <= 10; i++)
            {
                var cap = (i % 2 == 0) ? 4 : 2;
                tables.Add(new DiningTable
                {
                    TableNumber = $"L{i:D2}",
                    AreaId = areaFloor.Id,
                    Capacity = cap,
                    Status = TableStatus.Available
                });
            }

            // Phòng VIP: V01 - V05
            for (int i = 1; i <= 5; i++)
            {
                var cap = (i <= 2) ? 12 : 20;
                tables.Add(new DiningTable
                {
                    TableNumber = $"V{i:D2}",
                    AreaId = areaVip.Id,
                    Capacity = cap,
                    Status = TableStatus.Available
                });
            }

            context.DiningTables.AddRange(tables);
            await context.SaveChangesAsync();
        }

        // 2. Seed Categories & 13 Menu Items
        if (!await context.Categories.AnyAsync())
        {
            var catKhaiVi = new Category { CategoryName = "Khai vị", TargetKitchen = KitchenStation.ColdKitchen, DisplayOrder = 1 };
            var catMonChinh = new Category { CategoryName = "Món chính", TargetKitchen = KitchenStation.HotKitchen, DisplayOrder = 2 };
            var catTrangMieng = new Category { CategoryName = "Tráng miệng", TargetKitchen = KitchenStation.ColdKitchen, DisplayOrder = 3 };
            var catDoUong = new Category { CategoryName = "Đồ uống", TargetKitchen = KitchenStation.Bar, DisplayOrder = 4 };

            context.Categories.AddRange(catKhaiVi, catMonChinh, catTrangMieng, catDoUong);
            await context.SaveChangesAsync();

            var items = new List<MenuItem>
            {
                // Khai vị
                new() { ItemCode = "M01", ItemName = "Gỏi cuốn tôm thịt", CategoryId = catKhaiVi.Id, Price = 85000, Unit = "Dĩa", KitchenStation = KitchenStation.ColdKitchen, Description = "Tôm sú tươi, thịt ba chỉ, bún tươi và rau ghém chấm tương đậu" },
                new() { ItemCode = "M02", ItemName = "Chả giò Sen Việt", CategoryId = catKhaiVi.Id, Price = 95000, Unit = "Phần", KitchenStation = KitchenStation.HotKitchen, Description = "Chả giò chiên giòn rụm với nhân thịt cua và hạt sen" },
                new() { ItemCode = "M03", ItemName = "Nộm ngó sen tai heo", CategoryId = catKhaiVi.Id, Price = 110000, Unit = "Dĩa", KitchenStation = KitchenStation.ColdKitchen, Description = "Ngó sen giòn ngọt, tai heo sần sật bóp chua ngọt đậm đà" },

                // Món chính
                new() { ItemCode = "M04", ItemName = "Bò lúc lắc khoai tây", CategoryId = catMonChinh.Id, Price = 220000, Unit = "Phần", KitchenStation = KitchenStation.HotKitchen, Description = "Thăn bò Úc xào sốt tiêu đen, ớt chuông ăn kèm khoai tây chiên" },
                new() { ItemCode = "M05", ItemName = "Cá hồi nướng sốt chanh leo", CategoryId = catMonChinh.Id, Price = 280000, Unit = "Phần", KitchenStation = KitchenStation.HotKitchen, Description = "Filet cá hồi Nauy nướng bơ tỏi quyện sốt chanh leo chua thanh" },
                new() { ItemCode = "M06", ItemName = "Gà hấp lá chanh", CategoryId = catMonChinh.Id, Price = 190000, Unit = "Nửa con", KitchenStation = KitchenStation.HotKitchen, Description = "Gà ta thả vườn thịt chắc ngọt hấp lá chanh truyền thống" },
                new() { ItemCode = "M07", ItemName = "Sườn non nướng mật ong", CategoryId = catMonChinh.Id, Price = 240000, Unit = "Phần", KitchenStation = KitchenStation.HotKitchen, Description = "Sườn heo ướp mật ong hoa nhãn nướng than hoa thơm lừng" },
                new() { ItemCode = "M08", ItemName = "Cơm chiên hải sản", CategoryId = catMonChinh.Id, Price = 130000, Unit = "Thố", KitchenStation = KitchenStation.HotKitchen, Description = "Cơm hạt ngọc chiên vàng tơi xốp cùng tôm, mực tươi và trứng gà" },

                // Tráng miệng
                new() { ItemCode = "M09", ItemName = "Chè hạt sen long nhãn", CategoryId = catTrangMieng.Id, Price = 45000, Unit = "Chén", KitchenStation = KitchenStation.ColdKitchen, Description = "Hạt sen Huế ninh bở bọc trong cùi nhãn lồng thanh mát" },
                new() { ItemCode = "M10", ItemName = "Bánh flan cốt dừa", CategoryId = catTrangMieng.Id, Price = 35000, Unit = "Dĩa", KitchenStation = KitchenStation.ColdKitchen, Description = "Bánh flan mềm mịn thơm ngậy nước cốt dừa và cà phê" },
                new() { ItemCode = "M11", ItemName = "Trái cây thập cẩm", CategoryId = catTrangMieng.Id, Price = 60000, Unit = "Dĩa", KitchenStation = KitchenStation.ColdKitchen, Description = "Các loại trái cây tươi theo mùa phục vụ mát lạnh" },

                // Đồ uống
                new() { ItemCode = "M12", ItemName = "Trà sen vàng", CategoryId = catDoUong.Id, Price = 55000, Unit = "Ly", KitchenStation = KitchenStation.Bar, Description = "Trà ô long kết hợp hạt sen bùi bùi và kem sữa béo ngậy" },
                new() { ItemCode = "M13", ItemName = "Nước ép cam tươi", CategoryId = catDoUong.Id, Price = 50000, Unit = "Ly", KitchenStation = KitchenStation.Bar, Description = "Cam sành vắt tươi nguyên chất bổ sung vitamin C" }
            };

            context.MenuItems.AddRange(items);
            await context.SaveChangesAsync();
        }

        // 3. Seed Demo Staff Accounts
        if (!await context.Staffs.AnyAsync())
        {
            var hasher = new PasswordHasher<Staff>();

            var manager = new Staff
            {
                StaffCode = "NV0001",
                FullName = "Nguyễn Văn Quản Lý",
                Username = "quanly",
                Role = StaffRole.Manager,
                PhoneNumber = "0901234567",
                CitizenId = "079090000001",
                IsActive = true
            };
            manager.PasswordHash = hasher.HashPassword(manager, "Admin@123");
            manager.ApprovalPinHash = hasher.HashPassword(manager, "1234");

            var cashier = new Staff
            {
                StaffCode = "NV0002",
                FullName = "Trần Thị Thu Ngân",
                Username = "thungan",
                Role = StaffRole.Cashier,
                PhoneNumber = "0901234568",
                CitizenId = "079090000002",
                IsActive = true
            };
            cashier.PasswordHash = hasher.HashPassword(cashier, "Admin@123");

            var staff = new Staff
            {
                StaffCode = "NV0003",
                FullName = "Lê Văn Phục Vụ",
                Username = "phucvu",
                Role = StaffRole.Staff,
                PhoneNumber = "0901234569",
                CitizenId = "079090000003",
                IsActive = true
            };
            staff.PasswordHash = hasher.HashPassword(staff, "Admin@123");

            var chef = new Staff
            {
                StaffCode = "NV0004",
                FullName = "Hoàng Bếp Trưởng",
                Username = "daubep",
                Role = StaffRole.Kitchen,
                PhoneNumber = "0901234570",
                CitizenId = "079090000004",
                IsActive = true
            };
            chef.PasswordHash = hasher.HashPassword(chef, "Admin@123");

            context.Staffs.AddRange(manager, cashier, staff, chef);
            await context.SaveChangesAsync();
        }

        // 4. Seed Printers
        if (!await context.Printers.AnyAsync())
        {
            context.Printers.AddRange(
                new Printer { Name = "Máy in Bếp Nóng", Station = KitchenStation.HotKitchen, IpAddress = "127.0.0.1", Port = 9100, IsActive = true },
                new Printer { Name = "Máy in Bếp Lạnh", Station = KitchenStation.ColdKitchen, IpAddress = "127.0.0.1", Port = 9101, IsActive = true },
                new Printer { Name = "Máy in Quầy Bar", Station = KitchenStation.Bar, IpAddress = "127.0.0.1", Port = 9102, IsActive = true }
            );
            await context.SaveChangesAsync();
        }
    }
}
