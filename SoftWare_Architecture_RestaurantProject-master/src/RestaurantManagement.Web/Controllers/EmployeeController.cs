using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestaurantManagement.Business.DTOs;
using RestaurantManagement.Business.Services;
using RestaurantManagement.Web.Models;

namespace RestaurantManagement.Web.Controllers;

[Authorize(Policy = "ManagerOnly")]
public class EmployeeController : Controller
{
    private readonly IStaffService _staffService;
    private readonly ILogger<EmployeeController> _logger;

    public EmployeeController(
        IStaffService staffService,
        ILogger<EmployeeController> logger)
    {
        _staffService = staffService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var staffs = await _staffService.GetAllStaffsAsync();
        return View(new EmployeeListViewModel { Staffs = staffs });
    }

    [HttpGet]
    public IActionResult Create()
    {
        return View(new EmployeeFormViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(EmployeeFormViewModel model)
    {
        if (string.IsNullOrWhiteSpace(model.Password))
        {
            ModelState.AddModelError("Password", "Mật khẩu cho nhân viên mới không được để trống.");
        }

        if (!ModelState.IsValid)
            return View(model);

        var result = await _staffService.CreateStaffAsync(new CreateStaffDto(
            model.StaffCode,
            model.FullName,
            model.Username,
            model.Password!,
            model.Role,
            model.PhoneNumber,
            model.CitizenId,
            model.ManagerPin
        ));

        if (!result.IsSuccess)
        {
            ModelState.AddModelError(string.Empty, result.Error?.Description ?? "Không thể tạo nhân viên.");
            return View(model);
        }

        TempData["SuccessMessage"] = $"Đã tạo tài khoản cho nhân viên {model.FullName} ({model.StaffCode}).";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var result = await _staffService.GetStaffByIdAsync(id);
        if (!result.IsSuccess)
        {
            TempData["ErrorMessage"] = "Không tìm thấy thông tin nhân viên.";
            return RedirectToAction(nameof(Index));
        }

        var s = result.Value;
        var model = new EmployeeFormViewModel
        {
            Id = s.Id,
            StaffCode = s.StaffCode,
            FullName = s.FullName,
            Username = s.Username,
            Role = s.Role,
            PhoneNumber = s.PhoneNumber,
            CitizenId = s.CitizenId,
            IsActive = s.IsActive
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(EmployeeFormViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var result = await _staffService.UpdateStaffAsync(new UpdateStaffDto(
            model.Id!.Value,
            model.FullName,
            model.PhoneNumber,
            model.CitizenId,
            model.Role,
            model.IsActive,
            model.Password,
            model.ManagerPin
        ));

        if (!result.IsSuccess)
        {
            ModelState.AddModelError(string.Empty, result.Error?.Description ?? "Không thể cập nhật nhân viên.");
            return View(model);
        }

        TempData["SuccessMessage"] = $"Đã cập nhật nhân viên {model.FullName}.";
        return RedirectToAction(nameof(Index));
    }
}
