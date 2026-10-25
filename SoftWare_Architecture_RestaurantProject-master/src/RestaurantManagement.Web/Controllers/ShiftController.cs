using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestaurantManagement.Business.DTOs;
using RestaurantManagement.Business.Services;
using RestaurantManagement.Web.Models;

namespace RestaurantManagement.Web.Controllers;

[Authorize(Policy = "CashierOrManager")]
public class ShiftController : Controller
{
    private readonly IShiftService _shiftService;
    private readonly ILogger<ShiftController> _logger;

    public ShiftController(
        IShiftService shiftService,
        ILogger<ShiftController> logger)
    {
        _shiftService = shiftService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var activeShiftResult = await _shiftService.GetCurrentActiveShiftAsync();
        if (!activeShiftResult.IsSuccess)
        {
            return RedirectToAction(nameof(Open));
        }

        return View("Current", activeShiftResult.Value);
    }

    [HttpGet]
    public async Task<IActionResult> Open()
    {
        var activeShiftResult = await _shiftService.GetCurrentActiveShiftAsync();
        if (activeShiftResult.IsSuccess)
        {
            TempData["InfoMessage"] = "Hiện đang có một ca làm việc đang mở. Vui lòng kết ca hiện tại trước khi mở ca mới.";
            return RedirectToAction(nameof(Index));
        }

        var staffId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var staffName = User.FindFirstValue(ClaimTypes.GivenName) ?? User.Identity?.Name ?? "Thu ngân";

        var viewModel = new OpenShiftViewModel
        {
            StaffId = staffId,
            StaffName = staffName,
            InitialCash = 2000000m,
            TerminalId = "POS-01"
        };

        return View(viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Open(OpenShiftViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var staffId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var result = await _shiftService.OpenShiftAsync(new OpenShiftDto(
            staffId,
            model.InitialCash,
            model.TerminalId
        ));

        if (!result.IsSuccess)
        {
            ModelState.AddModelError(string.Empty, result.Error?.Description ?? "Không thể mở ca làm việc.");
            return View(model);
        }

        TempData["SuccessMessage"] = $"Đã mở ca {result.Value.ShiftCode} thành công. Tiền mặt ban đầu: {model.InitialCash:N0} VNĐ.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Close()
    {
        var activeShiftResult = await _shiftService.GetCurrentActiveShiftAsync();
        if (!activeShiftResult.IsSuccess)
        {
            TempData["ErrorMessage"] = "Không có ca làm việc nào đang mở để kết ca.";
            return RedirectToAction(nameof(Open));
        }

        var shift = activeShiftResult.Value;

        // Standard Vietnamese cash denominations: 500k, 200k, 100k, 50k, 20k, 10k, 5k, 2k, 1k
        var standardDenominations = new List<int> { 500000, 200000, 100000, 50000, 20000, 10000, 5000, 2000, 1000 };
        var denominations = standardDenominations.Select(d => new CashDenominationInput
        {
            Denomination = d,
            Quantity = 0
        }).ToList();

        var viewModel = new CloseShiftViewModel
        {
            Shift = shift,
            Denominations = denominations
        };

        return View(viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Close(CloseShiftViewModel model)
    {
        var staffId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var activeShiftResult = await _shiftService.GetCurrentActiveShiftAsync();
        if (!activeShiftResult.IsSuccess)
        {
            TempData["ErrorMessage"] = "Không tìm thấy ca làm việc đang mở.";
            return RedirectToAction(nameof(Open));
        }

        var shift = activeShiftResult.Value;
        model.Shift = shift;

        var dtoList = model.Denominations?
            .Where(d => d.Quantity > 0)
            .Select(d => new CashDenomination(d.Denomination, d.Quantity))
            .ToList() ?? new List<CashDenomination>();

        var result = await _shiftService.CloseShiftAsync(new CloseShiftDto(
            shift.Id,
            staffId,
            dtoList,
            model.DiscrepancyReason,
            model.ManagerPin
        ));

        if (!result.IsSuccess)
        {
            ModelState.AddModelError(string.Empty, result.Error?.Description ?? "Không thể kết ca.");
            return View(model);
        }

        TempData["SuccessMessage"] = $"Đã kết ca {shift.ShiftCode} thành công. Doanh thu tiền mặt: {result.Value.TotalCashCollected:N0} VNĐ, Chuyển khoản: {result.Value.TotalTransferCollected:N0} VNĐ.";
        return RedirectToAction(nameof(History));
    }

    [HttpGet]
    public async Task<IActionResult> History(DateOnly? date = null)
    {
        var history = await _shiftService.GetShiftHistoryAsync(date);
        return View(history);
    }
}
