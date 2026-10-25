using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestaurantManagement.Business.DTOs;
using RestaurantManagement.Business.Services;
using RestaurantManagement.Web.Models;

namespace RestaurantManagement.Web.Controllers;

[Authorize(Policy = "ManagerOnly")]
public class AttendanceController : Controller
{
    private readonly IStaffService _staffService;
    private readonly IAuthService _authService;
    private readonly ILogger<AttendanceController> _logger;

    public AttendanceController(
        IStaffService staffService,
        IAuthService authService,
        ILogger<AttendanceController> logger)
    {
        _staffService = staffService;
        _authService = authService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index(DateOnly? date = null)
    {
        var targetDate = date ?? DateOnly.FromDateTime(DateTime.Now);
        var attendances = await _staffService.GetAttendancesAsync(targetDate);

        var viewModel = new AttendanceListViewModel
        {
            SelectedDate = targetDate,
            Attendances = attendances
        };

        return View(viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(ApproveAttendanceViewModel model)
    {
        var managerId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        // Xác thực mã PIN Quản lý
        var pinVerify = await _authService.VerifyManagerPinAsync(managerId, model.ManagerPin);
        if (!pinVerify.IsSuccess || !pinVerify.Value)
        {
            TempData["ErrorMessage"] = "Mã PIN Quản lý không chính xác. Không thể duyệt giải trình.";
            return RedirectToAction(nameof(Index));
        }

        var result = await _staffService.ApproveAttendanceAsync(new ApproveAttendanceDto(
            model.AttendanceId,
            managerId,
            model.Reason
        ));

        if (!result.IsSuccess)
        {
            TempData["ErrorMessage"] = result.Error?.Description ?? "Không thể duyệt giải trình đi trễ.";
        }
        else
        {
            TempData["SuccessMessage"] = $"Đã duyệt giải trình đi trễ cho nhân viên {model.StaffName}.";
        }

        return RedirectToAction(nameof(Index));
    }
}
