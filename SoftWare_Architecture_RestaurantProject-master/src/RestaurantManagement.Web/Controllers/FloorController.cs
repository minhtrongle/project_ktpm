using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestaurantManagement.Business.DTOs;
using RestaurantManagement.Business.Services;
using RestaurantManagement.Web.Models;

namespace RestaurantManagement.Web.Controllers;

[Authorize]
public class FloorController : Controller
{
    private readonly ITableService _tableService;
    private readonly IBookingService _bookingService;
    private readonly ILogger<FloorController> _logger;

    public FloorController(
        ITableService tableService,
        IBookingService bookingService,
        ILogger<FloorController> logger)
    {
        _tableService = tableService;
        _bookingService = bookingService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index(int? areaId = null)
    {
        var floorPlan = await _tableService.GetFloorPlanAsync();
        var todayBookings = await _bookingService.GetTodayBookingsAsync();

        var viewModel = new FloorIndexViewModel
        {
            FloorPlan = floorPlan,
            TodayBookings = todayBookings,
            SelectedAreaId = areaId
        };

        return View(viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SeatWalkIn(SeatWalkInViewModel model)
    {
        if (!ModelState.IsValid)
        {
            TempData["ErrorMessage"] = "Thông tin xếp bàn không hợp lệ.";
            return RedirectToAction(nameof(Index));
        }

        var staffId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var result = await _tableService.SeatWalkInAsync(new SeatWalkInDto(
            model.TableId,
            staffId,
            model.GuestCount
        ));

        if (!result.IsSuccess)
        {
            TempData["ErrorMessage"] = result.Error?.Description ?? "Không thể xếp khách vào bàn.";
            return RedirectToAction(nameof(Index));
        }

        TempData["SuccessMessage"] = $"Đã xếp khách vào bàn {model.TableCode}.";
        return RedirectToAction("Table", "Order", new { tableId = model.TableId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CheckInBooking(CheckInBookingViewModel model)
    {
        var staffId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var result = await _tableService.CheckInBookingAsync(new CheckInBookingDto(
            model.BookingId,
            staffId
        ));

        if (!result.IsSuccess)
        {
            TempData["ErrorMessage"] = result.Error?.Description ?? "Không thể xác nhận khách vào bàn.";
            return RedirectToAction(nameof(Index));
        }

        TempData["SuccessMessage"] = $"Xác nhận khách đặt bàn {model.CustomerName} vào bàn {model.TableCode}.";
        return RedirectToAction("Table", "Order", new { tableId = model.TableId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CompleteCleaning(int tableId)
    {
        var result = await _tableService.CompleteCleaningAsync(tableId);
        if (!result.IsSuccess)
        {
            TempData["ErrorMessage"] = result.Error?.Description ?? "Không thể cập nhật dọn bàn.";
        }
        else
        {
            TempData["SuccessMessage"] = "Đã dọn dẹp xong bàn. Bàn sẵn sàng đón khách.";
        }

        return RedirectToAction(nameof(Index));
    }
}
