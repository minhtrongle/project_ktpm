using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestaurantManagement.Business.DTOs;
using RestaurantManagement.Business.DTOs.Auth;
using RestaurantManagement.Business.Services;
using RestaurantManagement.Common.Enums;
using RestaurantManagement.Web.Models;

namespace RestaurantManagement.Web.Controllers;

public class AccountController : Controller
{
    private readonly IAuthService _authService;
    private readonly IStaffService _staffService;
    private readonly ILogger<AccountController> _logger;

    public AccountController(
        IAuthService authService,
        IStaffService staffService,
        ILogger<AccountController> logger)
    {
        _authService = authService;
        _staffService = staffService;
        _logger = logger;
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToUserDefaultPage();
        }

        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var result = await _authService.LoginAsync(new LoginDto(model.Username, model.Password));
        if (!result.IsSuccess)
        {
            ModelState.AddModelError(string.Empty, result.Error?.Description ?? "Đăng nhập thất bại.");
            return View(model);
        }

        var staff = result.Value;

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, staff.Id.ToString()),
            new(ClaimTypes.Name, staff.Username),
            new(ClaimTypes.GivenName, staff.FullName),
            new(ClaimTypes.Role, staff.Role.ToString()),
            new("StaffCode", staff.StaffCode)
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

        // Record attendance on login (BM11)
        try
        {
            await _staffService.CheckInAttendanceAsync(new CheckInAttendanceDto(staff.Id));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to record automatic attendance for staff {StaffId}", staff.Id);
        }

        if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
        {
            return Redirect(model.ReturnUrl);
        }

        return RedirectToUserDefaultPage();
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        HttpContext.Session.Clear();
        return RedirectToAction("Login");
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult AccessDenied()
    {
        return View();
    }

    private IActionResult RedirectToUserDefaultPage()
    {
        if (User.IsInRole(nameof(StaffRole.Kitchen)))
            return RedirectToAction("Index", "Kitchen");

        return RedirectToAction("Index", "Floor");
    }
}
