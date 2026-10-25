using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestaurantManagement.Business.DTOs;
using RestaurantManagement.Business.Services;
using RestaurantManagement.Common.Enums;
using RestaurantManagement.Web.Models;

namespace RestaurantManagement.Web.Controllers;

[Authorize(Policy = "CashierOrManager")]
public class InvoiceController : Controller
{
    private readonly IBillingService _billingService;
    private readonly IShiftService _shiftService;
    private readonly ILogger<InvoiceController> _logger;

    public InvoiceController(
        IBillingService billingService,
        IShiftService shiftService,
        ILogger<InvoiceController> logger)
    {
        _billingService = billingService;
        _shiftService = shiftService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Checkout(int orderId, string? customerPhone = null)
    {
        // Kiểm tra xem thu ngân đã mở ca chưa
        var shiftResult = await _shiftService.GetCurrentActiveShiftAsync();
        if (!shiftResult.IsSuccess)
        {
            TempData["ErrorMessage"] = "Bạn cần mở ca làm việc (BM04) trước khi thanh toán và lập hóa đơn.";
            return RedirectToAction("Open", "Shift");
        }

        var invoiceResult = await _billingService.PrepareInvoiceAsync(new PrepareInvoiceDto(
            orderId,
            customerPhone
        ));

        if (!invoiceResult.IsSuccess)
        {
            TempData["ErrorMessage"] = invoiceResult.Error?.Description ?? "Không thể tạo hóa đơn thanh toán.";
            return RedirectToAction("Index", "Floor");
        }

        var invoice = invoiceResult.Value;

        // Sinh link VietQR động
        var amountInt = (long)invoice.FinalAmount;
        var qrContent = $"THANHTOAN {invoice.OrderCode}";
        var vietQrUrl = $"https://img.vietqr.io/image/MB-09123456789-compact2.png?amount={amountInt}&addInfo={Uri.EscapeDataString(qrContent)}&accountName=NHA%20HANG%20SEN%20VIET";

        var viewModel = new CheckoutViewModel
        {
            Invoice = invoice,
            OrderId = orderId,
            CustomerPhone = customerPhone,
            PaymentMethod = PaymentMethod.Cash,
            CashGiven = invoice.FinalAmount,
            FinalAmount = invoice.FinalAmount,
            VietQrUrl = vietQrUrl
        };

        return View(viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ProcessPayment(CheckoutViewModel model)
    {
        var staffId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var result = await _billingService.ProcessPaymentAsync(new ProcessPaymentDto(
            model.OrderId,
            staffId,
            model.PaymentMethod,
            model.PaymentMethod == PaymentMethod.Cash ? model.CashGiven : model.FinalAmount
        ));

        if (!result.IsSuccess)
        {
            TempData["ErrorMessage"] = result.Error?.Description ?? "Thanh toán thất bại.";
            return RedirectToAction(nameof(Checkout), new { orderId = model.OrderId, customerPhone = model.CustomerPhone });
        }

        TempData["SuccessMessage"] = $"Thanh toán thành công hóa đơn {result.Value.InvoiceCode}. Bàn đã chuyển sang trạng thái đang dọn dẹp.";
        return RedirectToAction(nameof(Detail), new { invoiceId = result.Value.Id });
    }

    [HttpGet]
    public async Task<IActionResult> Detail(int invoiceId)
    {
        var result = await _billingService.GetInvoiceByIdAsync(invoiceId);
        if (!result.IsSuccess)
        {
            TempData["ErrorMessage"] = "Không tìm thấy hóa đơn.";
            return RedirectToAction("Index", "Floor");
        }

        return View(result.Value);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "ManagerOnly")]
    public async Task<IActionResult> Cancel(CancelInvoiceViewModel model)
    {
        var result = await _billingService.CancelInvoiceAsync(
            model.InvoiceId,
            model.Reason,
            model.ManagerPin
        );

        if (!result.IsSuccess)
        {
            TempData["ErrorMessage"] = result.Error?.Description ?? "Không thể hủy hóa đơn.";
        }
        else
        {
            TempData["SuccessMessage"] = $"Đã hủy hóa đơn {model.InvoiceCode} thành công.";
        }

        return RedirectToAction(nameof(Detail), new { invoiceId = model.InvoiceId });
    }
}
