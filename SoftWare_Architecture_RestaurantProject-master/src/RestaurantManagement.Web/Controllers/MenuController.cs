using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestaurantManagement.Business.DTOs;
using RestaurantManagement.Business.Services;
using RestaurantManagement.Web.Models;

namespace RestaurantManagement.Web.Controllers;

[Authorize(Policy = "ManagerOnly")]
public class MenuController : Controller
{
    private readonly IMenuService _menuService;
    private readonly ILogger<MenuController> _logger;

    public MenuController(
        IMenuService menuService,
        ILogger<MenuController> logger)
    {
        _menuService = menuService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index(int? categoryId = null)
    {
        var categories = await _menuService.GetCategoriesAsync();
        var items = await _menuService.GetMenuItemsAsync(categoryId);

        var viewModel = new MenuManagementViewModel
        {
            Categories = categories,
            Items = items,
            SelectedCategoryId = categoryId
        };

        return View(viewModel);
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var categories = await _menuService.GetCategoriesAsync();
        ViewBag.Categories = categories;

        return View(new MenuItemFormViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(MenuItemFormViewModel model)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.Categories = await _menuService.GetCategoriesAsync();
            return View(model);
        }

        var result = await _menuService.CreateMenuItemAsync(new CreateMenuItemDto(
            model.ItemCode,
            model.Name,
            model.CategoryId,
            model.Price,
            model.Unit,
            model.Station,
            model.Description
        ));

        if (!result.IsSuccess)
        {
            ModelState.AddModelError(string.Empty, result.Error?.Description ?? "Không thể tạo món mới.");
            ViewBag.Categories = await _menuService.GetCategoriesAsync();
            return View(model);
        }

        TempData["SuccessMessage"] = $"Đã thêm món '{model.Name}' vào thực đơn.";
        return RedirectToAction(nameof(Index), new { categoryId = model.CategoryId });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var result = await _menuService.GetMenuItemByIdAsync(id);
        if (!result.IsSuccess)
        {
            TempData["ErrorMessage"] = "Không tìm thấy món ăn.";
            return RedirectToAction(nameof(Index));
        }

        var item = result.Value;
        ViewBag.Categories = await _menuService.GetCategoriesAsync();

        var model = new MenuItemFormViewModel
        {
            Id = item.Id,
            ItemCode = item.ItemCode,
            Name = item.ItemName,
            Description = item.Description,
            Price = item.Price,
            CategoryId = item.CategoryId,
            Station = item.KitchenStation,
            Unit = item.Unit ?? "Phần",
            IsAvailable = item.IsAvailable
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(MenuItemFormViewModel model, string? managerPin = null)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.Categories = await _menuService.GetCategoriesAsync();
            return View(model);
        }

        var result = await _menuService.UpdateMenuItemAsync(new UpdateMenuItemDto(
            model.Id!.Value,
            model.Name,
            model.CategoryId,
            model.Price,
            model.Unit,
            model.Station,
            model.Description,
            model.IsAvailable,
            managerPin
        ));

        if (!result.IsSuccess)
        {
            ModelState.AddModelError(string.Empty, result.Error?.Description ?? "Không thể cập nhật món ăn.");
            ViewBag.Categories = await _menuService.GetCategoriesAsync();
            return View(model);
        }

        TempData["SuccessMessage"] = $"Đã cập nhật món '{model.Name}'.";
        return RedirectToAction(nameof(Index), new { categoryId = model.CategoryId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdatePrice(UpdatePriceViewModel model)
    {
        var result = await _menuService.UpdateItemPriceAsync(new UpdateItemPriceDto(
            model.MenuItemId,
            model.NewPrice,
            "Cập nhật bảng giá thực đơn",
            model.ManagerPin
        ));

        if (!result.IsSuccess)
        {
            TempData["ErrorMessage"] = result.Error?.Description ?? "Không thể cập nhật giá.";
        }
        else
        {
            TempData["SuccessMessage"] = $"Đã điều chỉnh giá món '{model.ItemName}' sang {model.NewPrice:N0} VNĐ.";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleAvailability(int id)
    {
        var result = await _menuService.ToggleItemAvailabilityAsync(id);
        if (!result.IsSuccess)
        {
            TempData["ErrorMessage"] = result.Error?.Description ?? "Không thể thay đổi trạng thái món.";
        }
        else
        {
            TempData["SuccessMessage"] = "Đã cập nhật trạng thái món.";
        }

        return RedirectToAction(nameof(Index));
    }
}
