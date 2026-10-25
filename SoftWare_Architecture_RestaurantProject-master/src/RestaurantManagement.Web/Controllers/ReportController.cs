using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using RestaurantManagement.Business.Options;
using RestaurantManagement.Business.Services;
using RestaurantManagement.Web.Models;

namespace RestaurantManagement.Web.Controllers;

[Authorize(Policy = "ManagerOnly")]
public class ReportController : Controller
{
    private readonly IReportService _reportService;
    private readonly ILogger<ReportController> _logger;
    private readonly TimeZoneInfo _timeZone;

    public ReportController(
        IReportService reportService,
        ILogger<ReportController> logger,
        IOptions<RestaurantRulesOptions> options)
    {
        _reportService = reportService;
        _logger = logger;
        _timeZone = TimeZoneInfo.FindSystemTimeZoneById(options.Value.TimeZone);
    }

    [HttpGet]
    public IActionResult Index()
    {
        return RedirectToAction(nameof(Revenue));
    }

    [HttpGet]
    public async Task<IActionResult> Revenue(DateOnly? fromDate, DateOnly? toDate)
    {
        var today = GetRestaurantDate();
        var from = fromDate ?? today.AddDays(-30);
        var to = toDate ?? today;

        var report = await _reportService.GetRevenueReportAsync(from, to);

        var viewModel = new RevenueReportViewModel
        {
            FromDate = from,
            ToDate = to,
            Report = report
        };

        return View(viewModel);
    }

    [HttpGet]
    public async Task<IActionResult> ExportRevenueCsv(DateOnly? fromDate, DateOnly? toDate)
    {
        var today = GetRestaurantDate();
        var from = fromDate ?? today.AddDays(-30);
        var to = toDate ?? today;

        var report = await _reportService.GetRevenueReportAsync(from, to);

        var sb = new StringBuilder();
        sb.AppendLine("Ngày,Số lượng hóa đơn,Tổng tiền món,Phụ thu cao điểm,Thuế VAT (8%),Giảm giá thành viên,Thực thu");

        foreach (var day in report.DailyBreakdown)
        {
            sb.AppendLine($"{day.Date:dd/MM/yyyy},{day.InvoicesCount},{day.GrossAmount},{day.SurchargeAmount},{day.VatAmount},{day.DiscountAmount},{day.FinalAmount}");
        }

        sb.AppendLine();
        sb.AppendLine($"TỔNG CỘNG,{report.TotalInvoices},{report.GrossTotal},{report.SurchargeTotal},{report.VatTotal},{report.DiscountTotal},{report.NetTotal}");
        sb.AppendLine($"Tiền mặt: {report.CashTotal}, Chuyển khoản VietQR: {report.TransferTotal}");

        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
        return File(bytes, "text/csv", $"BaoCaoDoanhThu_{from:yyyyMMdd}_{to:yyyyMMdd}.csv");
    }

    [HttpGet]
    public async Task<IActionResult> BestSellers(DateOnly? fromDate, DateOnly? toDate)
    {
        var today = GetRestaurantDate();
        var from = fromDate ?? today.AddDays(-30);
        var to = toDate ?? today;

        var report = await _reportService.GetBestSellersReportAsync(from, to);

        var viewModel = new BestSellersReportViewModel
        {
            FromDate = from,
            ToDate = to,
            Report = report
        };

        return View(viewModel);
    }

    private DateOnly GetRestaurantDate()
    {
        var localNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _timeZone);
        return DateOnly.FromDateTime(localNow);
    }
}
