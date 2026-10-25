using Microsoft.EntityFrameworkCore;
using RestaurantManagement.Business.DTOs;
using RestaurantManagement.Common.Enums;
using RestaurantManagement.DataAccess.UnitOfWork;

namespace RestaurantManagement.Business.Services;

public interface IReportService
{
    Task<RevenueReportDto> GetRevenueReportAsync(DateOnly fromDate, DateOnly toDate, CancellationToken cancellationToken = default);
    Task<BestSellersReportDto> GetBestSellersReportAsync(DateOnly fromDate, DateOnly toDate, CancellationToken cancellationToken = default);
}

public class ReportService : IReportService
{
    private readonly IUnitOfWork _uow;

    public ReportService(IUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task<RevenueReportDto> GetRevenueReportAsync(DateOnly fromDate, DateOnly toDate, CancellationToken cancellationToken = default)
    {
        var fromUtc = fromDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var toUtc = toDate.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);

        var invoices = await _uow.Invoices.GetAll()
            .Where(i => i.Status == InvoiceStatus.Paid && i.CreatedAt >= fromUtc && i.CreatedAt <= toUtc)
            .OrderBy(i => i.CreatedAt)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var totalInvoices = invoices.Count;
        var grossTotal = invoices.Sum(i => i.TotalAmount);
        var surchargeTotal = invoices.Sum(i => i.SurchargeAmount);
        var vatTotal = invoices.Sum(i => i.VatAmount);
        var discountTotal = invoices.Sum(i => i.DiscountAmount);
        var netTotal = invoices.Sum(i => i.FinalAmount);
        var cashTotal = invoices.Where(i => i.PaymentMethod == PaymentMethod.Cash).Sum(i => i.FinalAmount);
        var transferTotal = invoices.Where(i => i.PaymentMethod == PaymentMethod.BankTransfer).Sum(i => i.FinalAmount);

        var dailyBreakdown = invoices
            .GroupBy(i => DateOnly.FromDateTime(i.CreatedAt))
            .Select(g => new DailyRevenueDto(
                g.Key,
                g.Count(),
                g.Sum(i => i.TotalAmount),
                g.Sum(i => i.SurchargeAmount),
                g.Sum(i => i.VatAmount),
                g.Sum(i => i.DiscountAmount),
                g.Sum(i => i.FinalAmount)
            ))
            .OrderBy(d => d.Date)
            .ToList();

        return new RevenueReportDto(
            fromDate,
            toDate,
            totalInvoices,
            grossTotal,
            surchargeTotal,
            vatTotal,
            discountTotal,
            netTotal,
            cashTotal,
            transferTotal,
            dailyBreakdown
        );
    }

    public async Task<BestSellersReportDto> GetBestSellersReportAsync(DateOnly fromDate, DateOnly toDate, CancellationToken cancellationToken = default)
    {
        var fromUtc = fromDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var toUtc = toDate.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);

        // Lấy tất cả món ăn trong thực đơn để đảm bảo không sót món có doanh số 0
        var allMenuItems = await _uow.MenuItems.GetAll()
            .Include(m => m.Category)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var soldDetails = await _uow.OrderDetails.GetAll()
            .Include(d => d.Order)
            .Where(d => d.Status != OrderItemStatus.Cancelled
                     && d.CreatedAt >= fromUtc
                     && d.CreatedAt <= toUtc
                     && d.Order.ClosedAt != null)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var salesByItem = soldDetails
            .GroupBy(d => d.MenuItemId)
            .ToDictionary(
                g => g.Key,
                g => new { Quantity = g.Sum(x => x.Quantity), Revenue = g.Sum(x => x.SubTotal) }
            );

        var fullSalesList = allMenuItems.Select(m =>
        {
            var hasSales = salesByItem.TryGetValue(m.Id, out var s);
            return new MenuItemSalesDto(
                m.Id,
                m.ItemCode,
                m.ItemName,
                m.Category?.CategoryName ?? string.Empty,
                hasSales ? s!.Quantity : 0,
                hasSales ? s!.Revenue : 0m
            );
        }).ToList();

        var topSellers = fullSalesList
            .OrderByDescending(s => s.QuantitySold)
            .ThenByDescending(s => s.TotalSales)
            .Take(10)
            .ToList();

        // Món tiêu thụ kém: dưới 5 phần
        var lowSales = fullSalesList
            .Where(s => s.QuantitySold < 5)
            .OrderBy(s => s.QuantitySold)
            .ThenBy(s => s.TotalSales)
            .ToList();

        return new BestSellersReportDto(
            fromDate,
            toDate,
            topSellers,
            lowSales
        );
    }
}
