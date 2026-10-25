using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestaurantManagement.Business.Services;
using RestaurantManagement.Web.Models;

namespace RestaurantManagement.Web.Controllers;

[AllowAnonymous]
public class HomeController : Controller
{
    private readonly IMenuService _menuService;
    private readonly ILogger<HomeController> _logger;

    public HomeController(
        IMenuService menuService,
        ILogger<HomeController> logger)
    {
        _menuService = menuService;
        _logger = logger;
    }

    public async Task<IActionResult> Index()
    {
        var featuredItems = await _menuService.GetMenuItemsAsync(onlyAvailable: true);
        ViewBag.FeaturedItems = featuredItems.Take(6).ToList();
        return View();
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
