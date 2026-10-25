namespace RestaurantManagement.Business.DTOs;

public record DailyRevenueDto(
    DateOnly Date,
    int InvoicesCount,
    decimal GrossAmount,
    decimal SurchargeAmount,
    decimal VatAmount,
    decimal DiscountAmount,
    decimal FinalAmount
);

public record RevenueReportDto(
    DateOnly FromDate,
    DateOnly ToDate,
    int TotalInvoices,
    decimal GrossTotal,
    decimal SurchargeTotal,
    decimal VatTotal,
    decimal DiscountTotal,
    decimal NetTotal,
    decimal CashTotal,
    decimal TransferTotal,
    List<DailyRevenueDto> DailyBreakdown
);

public record MenuItemSalesDto(
    int MenuItemId,
    string ItemCode,
    string ItemName,
    string CategoryName,
    int QuantitySold,
    decimal TotalSales
);

public record BestSellersReportDto(
    DateOnly FromDate,
    DateOnly ToDate,
    List<MenuItemSalesDto> TopSellers,
    List<MenuItemSalesDto> LowSales
);
