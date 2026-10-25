using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestaurantManagement.Business.DTOs;
using RestaurantManagement.Business.Services;
using RestaurantManagement.Web.Models;

namespace RestaurantManagement.Web.Controllers;

[AllowAnonymous]
public class BookingController : Controller
{
    private readonly IBookingService _bookingService;
    private readonly ITableService _tableService;
    private readonly ILogger<BookingController> _logger;

    public BookingController(
        IBookingService bookingService,
        ITableService tableService,
        ILogger<BookingController> logger)
    {
        _bookingService = bookingService;
        _tableService = tableService;
        _logger = logger;
    }

    [HttpGet]
    public IActionResult Index()
    {
        return RedirectToAction(nameof(Search));
    }

    // BM12: Tra cứu bàn trống
    [HttpGet]
    public async Task<IActionResult> Search(DateOnly? date, TimeOnly? time, int? guests, int? areaId)
    {
        var model = new SearchAvailableTablesViewModel
        {
            BookingDate = date ?? DateOnly.FromDateTime(DateTime.Now.AddDays(1)),
            BookingTime = time ?? new TimeOnly(18, 30),
            GuestCount = guests ?? 4,
            PreferredAreaId = areaId
        };

        if (date.HasValue && time.HasValue)
        {
            var result = await _bookingService.SearchAvailableTablesAsync(new SearchAvailableTablesQueryDto(
                model.BookingDate,
                model.BookingTime,
                model.GuestCount,
                model.PreferredAreaId
            ));

            if (result.IsSuccess)
            {
                model.AvailableTables = result.Value;
                model.HasSearched = true;
            }
            else
            {
                TempData["ErrorMessage"] = result.Error?.Description ?? "Không tìm thấy bàn phù hợp.";
            }
        }

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Search(SearchAvailableTablesViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var result = await _bookingService.SearchAvailableTablesAsync(new SearchAvailableTablesQueryDto(
            model.BookingDate,
            model.BookingTime,
            model.GuestCount,
            model.PreferredAreaId
        ));

        model.HasSearched = true;
        if (result.IsSuccess)
        {
            model.AvailableTables = result.Value;
        }
        else
        {
            ModelState.AddModelError(string.Empty, result.Error?.Description ?? "Không tìm thấy bàn phù hợp.");
        }

        return View(model);
    }

    // BM13: Phiếu đăng ký đặt bàn
    [HttpGet]
    public async Task<IActionResult> Create(int tableId, string date, string time, int guests)
    {
        var tableResult = await _tableService.GetTableByIdAsync(tableId);
        if (!tableResult.IsSuccess)
        {
            TempData["ErrorMessage"] = "Bàn không tồn tại.";
            return RedirectToAction(nameof(Search));
        }

        var table = tableResult.Value;
        var bookingDate = DateOnly.Parse(date);
        var bookingTime = TimeOnly.Parse(time);

        // Tiền cọc: < 6 người -> 200k, >= 6 người -> 500k
        var deposit = guests >= 6 ? 500000m : 200000m;

        var model = new CreateBookingViewModel
        {
            TableId = table.Id,
            TableCode = table.TableNumber,
            AreaName = table.AreaName,
            Capacity = table.Capacity,
            BookingDate = bookingDate,
            BookingTime = bookingTime,
            GuestCount = guests,
            DepositAmount = deposit
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateBookingViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var result = await _bookingService.CreateBookingAsync(new CreateBookingDto(
            model.TableId,
            model.CustomerName,
            model.PhoneNumber,
            model.Email,
            model.BookingDate,
            model.BookingTime,
            model.GuestCount,
            model.SpecialRequests
        ));

        if (!result.IsSuccess)
        {
            ModelState.AddModelError(string.Empty, result.Error?.Description ?? "Không thể đặt bàn.");
            return View(model);
        }

        TempData["SuccessMessage"] = "Đã tạo phiếu đặt bàn thành công! Vui lòng thanh toán cọc trong vòng 15 phút để giữ bàn.";
        return RedirectToAction(nameof(Payment), new { bookingId = result.Value.BookingId });
    }

    // BM14: Xác nhận đặt bàn & Thanh toán cọc trực tuyến
    [HttpGet]
    public async Task<IActionResult> Payment(int bookingId)
    {
        var bookingResult = await _bookingService.GetBookingByIdAsync(bookingId);
        if (!bookingResult.IsSuccess)
        {
            TempData["ErrorMessage"] = bookingResult.Error?.Description ?? "Không tìm thấy thông tin đặt bàn hoặc thời hạn giữ chỗ đã kết thúc.";
            return RedirectToAction(nameof(Search));
        }

        var booking = bookingResult.Value;
        var amountInt = (long)booking.DepositAmount;
        var qrContent = $"COC {booking.BookingCode}";
        var vietQrUrl = $"https://img.vietqr.io/image/MB-09123456789-compact2.png?amount={amountInt}&addInfo={Uri.EscapeDataString(qrContent)}&accountName=NHA%20HANG%20SEN%20VIET";

        var viewModel = new BookingConfirmationViewModel
        {
            Booking = booking,
            VietQrUrl = vietQrUrl,
            HoldRemainingMinutes = 15
        };

        return View(viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ConfirmPayment(int bookingId, string transactionRef)
    {
        if (string.IsNullOrWhiteSpace(transactionRef))
            transactionRef = $"TXN-{Guid.NewGuid():N}";

        var result = await _bookingService.ConfirmBookingPaymentAsync(
            bookingId,
            transactionRef,
            "VietQR_Sandbox"
        );

        if (!result.IsSuccess)
        {
            TempData["ErrorMessage"] = result.Error?.Description ?? "Xác nhận thanh toán cọc thất bại.";
            return RedirectToAction(nameof(Payment), new { bookingId });
        }

        TempData["SuccessMessage"] = $"Thanh toán cọc thành công! Bàn của quý khách đã được xác nhận (Mã đặt bàn: {result.Value.BookingCode}).";
        return RedirectToAction(nameof(Lookup), new { bookingCode = result.Value.BookingCode, phoneNumber = result.Value.PhoneNumber });
    }

    // BM15: Tra cứu & Hủy đặt bàn
    [HttpGet]
    public async Task<IActionResult> Lookup(string? bookingCode = null, string? phoneNumber = null)
    {
        var model = new LookupBookingViewModel
        {
            BookingCode = bookingCode,
            PhoneNumber = phoneNumber
        };

        if (!string.IsNullOrWhiteSpace(bookingCode) && !string.IsNullOrWhiteSpace(phoneNumber))
        {
            var result = await _bookingService.GetBookingByCodeAsync(bookingCode.Trim(), phoneNumber.Trim());
            model.HasSearched = true;

            if (result.IsSuccess)
            {
                model.Booking = result.Value;

                // Tính toán chính sách hoàn cọc ước lượng
                var now = DateTime.UtcNow;
                var hoursUntilBooking = (result.Value.BookingDateTimeUtc - now).TotalHours;

                if (hoursUntilBooking >= 2)
                {
                    model.RefundPercentage = 100;
                    model.EstimatedRefund = result.Value.DepositAmount;
                    model.RefundPolicyNote = "Hủy trước 2 tiếng: Hoàn lại 100% tiền cọc.";
                }
                else if (hoursUntilBooking > 0)
                {
                    model.RefundPercentage = 50;
                    model.EstimatedRefund = Math.Round(result.Value.DepositAmount * 0.5m, 0);
                    model.RefundPolicyNote = "Hủy cận giờ (trong vòng 2 tiếng): Hoàn lại 50% tiền cọc.";
                }
                else
                {
                    model.RefundPercentage = 0;
                    model.EstimatedRefund = 0;
                    model.RefundPolicyNote = "Hủy sau giờ hẹn hoặc quá giờ: Không hoàn tiền cọc.";
                }
            }
            else
            {
                TempData["ErrorMessage"] = result.Error?.Description ?? "Không tìm thấy thông tin đặt bàn với mã và SĐT đã nhập.";
            }
        }

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(string bookingCode, string phoneNumber, string? reason)
    {
        var result = await _bookingService.CancelBookingAsync(new CancelBookingDto(
            bookingCode,
            phoneNumber,
            reason ?? "Khách hàng yêu cầu hủy bàn trực tuyến"
        ));

        if (!result.IsSuccess)
        {
            TempData["ErrorMessage"] = result.Error?.Description ?? "Không thể hủy đặt bàn.";
            return RedirectToAction(nameof(Lookup), new { bookingCode, phoneNumber });
        }

        var cancelInfo = result.Value;
        TempData["SuccessMessage"] = $"Đã hủy đặt bàn {cancelInfo.BookingCode}. Tiền cọc: {cancelInfo.DepositAmount:N0}đ, Tỷ lệ hoàn: {cancelInfo.RefundRate * 100}%, Số tiền hoàn: {cancelInfo.RefundAmount:N0}đ ({cancelInfo.Explanation}).";

        return RedirectToAction(nameof(Lookup), new { bookingCode, phoneNumber });
    }
}
