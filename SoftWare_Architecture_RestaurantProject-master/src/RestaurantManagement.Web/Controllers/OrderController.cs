using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestaurantManagement.Business.DTOs;
using RestaurantManagement.Business.Services;
using RestaurantManagement.Web.Models;

namespace RestaurantManagement.Web.Controllers;

[Authorize]
public class OrderController : Controller
{
    private readonly IOrderService _orderService;
    private readonly ITableService _tableService;
    private readonly IMenuService _menuService;
    private readonly ILogger<OrderController> _logger;

    public OrderController(
        IOrderService orderService,
        ITableService tableService,
        IMenuService menuService,
        ILogger<OrderController> logger)
    {
        _orderService = orderService;
        _tableService = tableService;
        _menuService = menuService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Table(int tableId, int? categoryId = null)
    {
        var tableResult = await _tableService.GetTableByIdAsync(tableId);
        if (!tableResult.IsSuccess)
        {
            TempData["ErrorMessage"] = "Không tìm thấy thông tin bàn.";
            return RedirectToAction("Index", "Floor");
        }

        var orderResult = await _orderService.GetActiveOrderByTableIdAsync(tableId);
        if (!orderResult.IsSuccess)
        {
            TempData["ErrorMessage"] = "Bàn này hiện chưa có order đang mở. Vui lòng xếp khách trước.";
            return RedirectToAction("Index", "Floor");
        }

        var categories = await _menuService.GetCategoriesAsync();
        var menuItems = await _menuService.GetMenuItemsAsync(categoryId, onlyAvailable: true);

        var viewModel = new OrderTableViewModel
        {
            Table = tableResult.Value,
            Order = orderResult.Value,
            Categories = categories,
            MenuItems = menuItems,
            SelectedCategoryId = categoryId
        };

        return View(viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddItem(AddOrderItemViewModel model)
    {
        if (model.Quantity <= 0)
            model.Quantity = 1;

        var result = await _orderService.AddOrderItemAsync(new AddOrderItemDto(
            model.OrderId,
            model.MenuItemId,
            model.Quantity,
            model.Note
        ));

        if (!result.IsSuccess)
        {
            TempData["ErrorMessage"] = result.Error?.Description ?? "Không thể thêm món vào order.";
        }
        else
        {
            TempData["SuccessMessage"] = "Đã thêm món vào danh sách gọi.";
        }

        var orderResult = await _orderService.GetOrderByIdAsync(model.OrderId);
        if (orderResult.IsSuccess)
        {
            return RedirectToAction(nameof(Table), new { tableId = orderResult.Value.TableId });
        }

        return RedirectToAction("Index", "Floor");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateQuantity(int orderDetailId, int orderId, int quantity, string? note)
    {
        var result = await _orderService.UpdateOrderItemQuantityAsync(orderDetailId, quantity, note);
        if (!result.IsSuccess)
        {
            TempData["ErrorMessage"] = result.Error?.Description ?? "Không thể cập nhật số lượng món.";
        }

        var orderResult = await _orderService.GetOrderByIdAsync(orderId);
        if (orderResult.IsSuccess)
        {
            return RedirectToAction(nameof(Table), new { tableId = orderResult.Value.TableId });
        }

        return RedirectToAction("Index", "Floor");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SendToKitchen(int orderId)
    {
        var result = await _orderService.SendToKitchenAsync(orderId);
        if (!result.IsSuccess)
        {
            TempData["ErrorMessage"] = result.Error?.Description ?? "Không thể gửi order xuống bếp.";
        }
        else
        {
            var ticketCount = result.Value.TicketCodes.Count;
            TempData["SuccessMessage"] = $"Đã in & gửi thành công {result.Value.ItemCount} món tới {ticketCount} trạm bếp (KOT: {string.Join(", ", result.Value.TicketCodes)}).";
        }

        var orderResult = await _orderService.GetOrderByIdAsync(orderId);
        if (orderResult.IsSuccess)
        {
            return RedirectToAction(nameof(Table), new { tableId = orderResult.Value.TableId });
        }

        return RedirectToAction("Index", "Floor");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeItem(ChangeOrderItemViewModel model)
    {
        var result = await _orderService.ChangeOrderItemAsync(new ChangeOrderItemDto(
            model.OrderDetailId,
            model.NewMenuItemId,
            1,
            model.Reason,
            model.ManagerPin
        ));

        if (!result.IsSuccess)
        {
            TempData["ErrorMessage"] = result.Error?.Description ?? "Không thể đổi món.";
        }
        else
        {
            TempData["SuccessMessage"] = "Đã thực hiện đổi món thành công.";
        }

        var orderResult = await _orderService.GetOrderByIdAsync(model.OrderId);
        if (orderResult.IsSuccess)
        {
            return RedirectToAction(nameof(Table), new { tableId = orderResult.Value.TableId });
        }

        return RedirectToAction("Index", "Floor");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReturnItem(ReturnOrderItemViewModel model)
    {
        var result = await _orderService.ReturnOrderItemAsync(new ReturnOrderItemDto(
            model.OrderDetailId,
            model.Reason,
            RestaurantFault: true,
            ManagerPin: model.ManagerPin
        ));

        if (!result.IsSuccess)
        {
            TempData["ErrorMessage"] = result.Error?.Description ?? "Không thể hủy/trả món.";
        }
        else
        {
            TempData["SuccessMessage"] = "Đã hủy món thành công với phê duyệt của Quản lý.";
        }

        var orderResult = await _orderService.GetOrderByIdAsync(model.OrderId);
        if (orderResult.IsSuccess)
        {
            return RedirectToAction(nameof(Table), new { tableId = orderResult.Value.TableId });
        }

        return RedirectToAction("Index", "Floor");
    }

    [HttpGet]
    public async Task<IActionResult> PreBill(int orderId)
    {
        var preBillResult = await _orderService.GetPreBillAsync(orderId);
        if (!preBillResult.IsSuccess)
        {
            TempData["ErrorMessage"] = preBillResult.Error?.Description ?? "Không thể lập phiếu tạm tính.";
            return RedirectToAction("Index", "Floor");
        }

        return View(new PreBillViewModel { PreBill = preBillResult.Value });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RequestPayment(int orderId)
    {
        var result = await _orderService.RequestPaymentAsync(orderId);
        if (!result.IsSuccess)
        {
            TempData["ErrorMessage"] = result.Error?.Description ?? "Không thể gửi yêu cầu tính tiền.";
        }
        else
        {
            TempData["SuccessMessage"] = "Đã thông báo cho Thu ngân sẵn sàng thanh toán.";
        }

        return RedirectToAction(nameof(PreBill), new { orderId });
    }
}
