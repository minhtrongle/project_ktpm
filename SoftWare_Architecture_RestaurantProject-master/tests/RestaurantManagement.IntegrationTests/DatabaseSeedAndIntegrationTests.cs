using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RestaurantManagement.Business;
using RestaurantManagement.Business.DTOs;
using RestaurantManagement.Business.Services;
using RestaurantManagement.Common.Enums;
using RestaurantManagement.DataAccess.Context;
using Xunit;

namespace RestaurantManagement.IntegrationTests;

public class DatabaseSeedAndIntegrationTests
{
    private readonly IServiceProvider _serviceProvider;

    public DatabaseSeedAndIntegrationTests()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Host=127.0.0.1;Port=4321;Database=restaurant_db;Username=postgres;Password=123",
                ["RestaurantRules:TimeZone"] = "Asia/Ho_Chi_Minh",
                ["RestaurantRules:Deposit:Small"] = "200000",
                ["RestaurantRules:Deposit:Large"] = "500000",
                ["RestaurantRules:Deposit:LargeGroupFrom"] = "6",
                ["RestaurantRules:Booking:HoldMinutes"] = "15",
                ["RestaurantRules:Booking:SlotHours"] = "2",
                ["RestaurantRules:Booking:MinLeadMinutes"] = "60",
                ["RestaurantRules:Billing:VatRate"] = "0.08",
                ["RestaurantRules:Billing:PeakSurcharge"] = "50000",
                ["RestaurantRules:Billing:MembershipDiscount:Silver"] = "0.05",
                ["RestaurantRules:Billing:MembershipDiscount:Gold"] = "0.10",
                ["RestaurantRules:Billing:MembershipDiscount:Diamond"] = "0.15",
                ["RestaurantRules:Shift:DefaultInitialCash"] = "2000000"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddBusinessLayer(configuration);

        _serviceProvider = services.BuildServiceProvider();
    }

    [Fact]
    public async Task SeedDatabase_And_ExecuteFullBookingOrderPaymentCycle()
    {
        using var scope = _serviceProvider.CreateScope();
        var sp = scope.ServiceProvider;

        // 1. Chạy DatabaseInitializer để seed đầy đủ 40 bàn, menu, nhân viên
        var initializer = sp.GetRequiredService<IDatabaseInitializer>();
        await initializer.InitializeAsync();

        var dbContext = sp.GetRequiredService<RestaurantDbContext>();

        // Kiểm tra số lượng bàn và khu vực đã seed
        var areasCount = await dbContext.Areas.CountAsync();
        var tablesCount = await dbContext.DiningTables.CountAsync();
        var menuCount = await dbContext.MenuItems.CountAsync();
        var staffCount = await dbContext.Staffs.CountAsync();

        Assert.Equal(4, areasCount);
        Assert.Equal(40, tablesCount);
        Assert.True(menuCount >= 13, "Menu items must be at least 13");
        Assert.True(staffCount >= 4, "Staff count must be at least 4");

        // 2. Kiểm tra TableService lấy Floor plan 40 bàn
        var tableService = sp.GetRequiredService<ITableService>();
        var floorPlan = await tableService.GetFloorPlanAsync();
        Assert.Equal(4, floorPlan.Count);
        var totalTablesInPlan = floorPlan.Sum(a => a.Tables.Count);
        Assert.Equal(40, totalTablesInPlan);

        // 3. Khách hàng tra cứu bàn trống cho 4 người vào ngày mai lúc 18:30 (BM12)
        var bookingService = sp.GetRequiredService<IBookingService>();
        var tomorrow = DateOnly.FromDateTime(DateTime.Now.AddDays(1));
        var bookingTime = new TimeOnly(18, 30);

        var searchResult = await bookingService.SearchAvailableTablesAsync(new SearchAvailableTablesQueryDto(
            tomorrow,
            bookingTime,
            4
        ));

        Assert.True(searchResult.IsSuccess);
        Assert.NotEmpty(searchResult.Value);

        var chosenTable = searchResult.Value.First();

        // 4. Khách tạo yêu cầu đặt bàn (BM13)
        var bookingCreateResult = await bookingService.CreateBookingAsync(new CreateBookingDto(
            chosenTable.TableId,
            "Nguyễn Minh Anh",
            "0912345678",
            "minhanh@example.com",
            tomorrow,
            bookingTime,
            4,
            "Bàn tiệc sinh nhật cạnh cửa sổ"
        ));

        Assert.True(bookingCreateResult.IsSuccess);
        var booking = bookingCreateResult.Value;
        Assert.Equal(200000m, booking.DepositAmount); // 4 người < 6 -> cọc 200k

        // 5. Khách thanh toán cọc trực tuyến (BM14)
        var paymentConfirmResult = await bookingService.ConfirmBookingPaymentAsync(
            booking.BookingId,
            $"TXN-{Guid.NewGuid():N}",
            "VietQR_Sandbox"
        );

        Assert.True(paymentConfirmResult.IsSuccess);
        Assert.Equal(BookingStatus.Confirmed, paymentConfirmResult.Value.Status);

        // 6. Đến ngày giờ hẹn, Phục vụ xác nhận khách vào bàn (BM03)
        var waiter = await dbContext.Staffs.FirstAsync(s => s.Role == StaffRole.Staff);
        var checkInResult = await tableService.CheckInBookingAsync(new CheckInBookingDto(
            booking.BookingId,
            waiter.Id
        ));

        Assert.True(checkInResult.IsSuccess);
        var order = checkInResult.Value;
        Assert.Equal(TableStatus.Occupied, order.TableStatus);

        // 7. Nhân viên ghi nhận gọi món tại bàn (BM01)
        var orderService = sp.GetRequiredService<IOrderService>();
        var goiCuon = await dbContext.MenuItems.FirstAsync(m => m.ItemCode == "M01"); // Khai vị - Cold
        var boLucLac = await dbContext.MenuItems.FirstAsync(m => m.ItemCode == "M04"); // Món chính - Hot
        var traSen = await dbContext.MenuItems.FirstAsync(m => m.ItemCode == "M12"); // Đồ uống - Bar

        var addItem1 = await orderService.AddOrderItemAsync(new AddOrderItemDto(order.Id, goiCuon.Id, 2, "Ít cay"));
        var addItem2 = await orderService.AddOrderItemAsync(new AddOrderItemDto(order.Id, boLucLac.Id, 2, "Chín vừa"));
        var addItem3 = await orderService.AddOrderItemAsync(new AddOrderItemDto(order.Id, traSen.Id, 4, "Ít đá"));

        Assert.True(addItem1.IsSuccess);
        Assert.True(addItem2.IsSuccess);
        Assert.True(addItem3.IsSuccess);

        // 8. Bấm gửi bếp in KOT (BM01)
        var kotResult = await orderService.SendToKitchenAsync(order.Id);
        Assert.True(kotResult.IsSuccess);
        Assert.Equal(3, kotResult.Value.TicketCodes.Count); // 3 trạm: HotKitchen, ColdKitchen, Bar

        // 9. Phục vụ in phiếu tạm tính tiền (BM02)
        var preBillResult = await orderService.GetPreBillAsync(order.Id);
        Assert.True(preBillResult.IsSuccess);
        var preBill = preBillResult.Value;
        Assert.Equal(200000m, preBill.DepositDeducted); // Trừ tiền cọc

        // 10. Thu ngân mở ca (BM04)
        var cashier = await dbContext.Staffs.FirstAsync(s => s.Role == StaffRole.Cashier);
        var shiftService = sp.GetRequiredService<IShiftService>();

        // Đóng ca cũ nếu còn active để mở ca test mới
        var activeShift = await shiftService.GetCurrentActiveShiftAsync();
        if (activeShift.IsSuccess)
        {
            await shiftService.CloseShiftAsync(new CloseShiftDto(
                activeShift.Value.Id,
                cashier.Id,
                new List<CashDenomination> { new(500000, 4) }, // 2M
                "Đóng ca cũ để chạy integration test"
            ));
        }

        var openShiftResult = await shiftService.OpenShiftAsync(new OpenShiftDto(cashier.Id, 2000000, "POS-01"));
        Assert.True(openShiftResult.IsSuccess);

        // 11. Thu ngân lập hóa đơn thanh toán (BM05)
        var billingService = sp.GetRequiredService<IBillingService>();
        var prepInvoiceResult = await billingService.PrepareInvoiceAsync(new PrepareInvoiceDto(
            order.Id,
            "0912345678" // Khách Nguyễn Minh Anh
        ));
        Assert.True(prepInvoiceResult.IsSuccess);

        // 12. Thu ngân xác nhận thu tiền và đóng bàn (BM05)
        var payResult = await billingService.ProcessPaymentAsync(new ProcessPaymentDto(
            order.Id,
            cashier.Id,
            PaymentMethod.BankTransfer,
            prepInvoiceResult.Value.FinalAmount
        ));
        Assert.True(payResult.IsSuccess);

        // Bàn phải chuyển sang trạng thái Cleaning
        var tableAfterPay = await tableService.GetTableByIdAsync(chosenTable.TableId);
        Assert.True(tableAfterPay.IsSuccess);
        Assert.Equal(TableStatus.Cleaning, tableAfterPay.Value.Status);

        // 13. Phục vụ dọn bàn xong bấm hoàn tất (BM03)
        var cleanResult = await tableService.CompleteCleaningAsync(chosenTable.TableId);
        Assert.True(cleanResult.IsSuccess);

        var tableAfterClean = await tableService.GetTableByIdAsync(chosenTable.TableId);
        Assert.True(tableAfterClean.IsSuccess);
        Assert.Equal(TableStatus.Available, tableAfterClean.Value.Status);
    }
}
