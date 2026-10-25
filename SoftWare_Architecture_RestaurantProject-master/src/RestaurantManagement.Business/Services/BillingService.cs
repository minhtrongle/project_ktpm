using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RestaurantManagement.Business.DTOs;
using RestaurantManagement.Business.Options;
using RestaurantManagement.Business.Policies;
using RestaurantManagement.Common.Enums;
using RestaurantManagement.Common.Results;
using RestaurantManagement.DataAccess.Entities;
using RestaurantManagement.DataAccess.UnitOfWork;

namespace RestaurantManagement.Business.Services;

public interface IBillingService
{
    Task<Result<InvoiceDto>> PrepareInvoiceAsync(PrepareInvoiceDto dto, CancellationToken cancellationToken = default);
    Task<Result<InvoiceDto>> ProcessPaymentAsync(ProcessPaymentDto dto, CancellationToken cancellationToken = default);
    Task<Result<InvoiceDto>> GetInvoiceByIdAsync(int invoiceId, CancellationToken cancellationToken = default);
    Task<Result<InvoiceDto>> GetInvoiceByOrderIdAsync(int orderId, CancellationToken cancellationToken = default);
    Task<Result> CancelInvoiceAsync(int invoiceId, string reason, string managerPin, CancellationToken cancellationToken = default);
}

public class BillingService : IBillingService
{
    private readonly IUnitOfWork _uow;
    private readonly BillingCalculator _billingCalculator;
    private readonly IPasswordHasher<Staff> _passwordHasher;
    private readonly RestaurantRulesOptions _options;

    public BillingService(
        IUnitOfWork uow,
        BillingCalculator billingCalculator,
        IPasswordHasher<Staff> passwordHasher,
        IOptions<RestaurantRulesOptions> options)
    {
        _uow = uow;
        _billingCalculator = billingCalculator;
        _passwordHasher = passwordHasher;
        _options = options.Value;
    }

    public async Task<Result<InvoiceDto>> PrepareInvoiceAsync(PrepareInvoiceDto dto, CancellationToken cancellationToken = default)
    {
        var order = await _uow.Orders.GetAll()
            .Include(o => o.Table)
                .ThenInclude(t => t.Area)
            .Include(o => o.Booking)
                .ThenInclude(b => b!.Customer)
            .Include(o => o.CreatedBy)
            .Include(o => o.OrderDetails)
            .FirstOrDefaultAsync(o => o.Id == dto.OrderId, cancellationToken);

        if (order == null)
            return Result<InvoiceDto>.Failure(Error.NotFound("Order.NotFound", "Không tìm thấy đơn gọi món."));

        if (order.ClosedAt != null)
            return Result<InvoiceDto>.Failure(Error.Validation("Order.Closed", "Đơn gọi món đã thanh toán đóng bàn trước đó."));

        var activeDetails = order.OrderDetails.Where(d => d.Status != OrderItemStatus.Cancelled).ToList();
        var itemsTotal = activeDetails.Sum(d => d.SubTotal);

        Customer? customer = null;
        var phone = dto.CustomerPhoneNumber?.Trim();

        if (!string.IsNullOrWhiteSpace(phone))
        {
            customer = await _uow.Customers.GetAll()
                .FirstOrDefaultAsync(c => c.PhoneNumber == phone, cancellationToken);
        }
        else if (order.Booking?.Customer != null)
        {
            customer = order.Booking.Customer;
        }

        var rank = customer?.Rank ?? MembershipRank.Standard;
        var deposit = order.Booking?.DepositAmount ?? 0m;

        var breakdown = _billingCalculator.Calculate(
            itemsTotal,
            order.CreatedAt,
            rank,
            dto.ManualDiscountPercent,
            deposit
        );

        var existingInvoice = await _uow.Invoices.GetAll()
            .FirstOrDefaultAsync(i => i.OrderId == order.Id && i.Status == InvoiceStatus.Paid, cancellationToken);

        var nowUtc = DateTime.UtcNow;
        var invoiceCode = $"INV-{nowUtc:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpper()}";
        var vietQrContent = $"https://img.vietqr.io/image/970436-123456789-compact2.png?amount={Convert.ToInt64(breakdown.FinalAmount)}&addInfo={invoiceCode}&accountName=NHA%20HANG%20SEN%20VIET";

        var manualDiscountAmount = BillingCalculator.Round(breakdown.ItemsTotal * dto.ManualDiscountPercent);

        // Chuẩn bị DTO preview hóa đơn
        return Result<InvoiceDto>.Success(new InvoiceDto(
            0,
            invoiceCode,
            order.Id,
            order.OrderCode,
            order.Table.TableNumber,
            order.Table.Area.AreaName,
            customer?.CustomerName ?? order.Booking?.CustomerName,
            customer?.PhoneNumber ?? order.Booking?.PhoneNumber,
            rank,
            breakdown.ItemsTotal,
            breakdown.SurchargeAmount,
            breakdown.VatAmount,
            breakdown.DiscountAmount - manualDiscountAmount,
            manualDiscountAmount,
            breakdown.DepositDeducted,
            breakdown.FinalAmount,
            PaymentMethod.Cash,
            InvoiceStatus.Paid,
            nowUtc,
            null,
            null,
            null,
            vietQrContent,
            order.CreatedBy.FullName
        ));
    }

    public async Task<Result<InvoiceDto>> ProcessPaymentAsync(ProcessPaymentDto dto, CancellationToken cancellationToken = default)
    {
        var order = await _uow.Orders.GetAll()
            .Include(o => o.Table)
                .ThenInclude(t => t.Area)
            .Include(o => o.Booking)
                .ThenInclude(b => b!.Customer)
            .Include(o => o.CreatedBy)
            .Include(o => o.OrderDetails)
            .FirstOrDefaultAsync(o => o.Id == dto.InvoiceId, cancellationToken);

        // Lưu ý: dto.InvoiceId ở đây truyền OrderId nếu chưa lưu Invoice
        Invoice? invoice = null;
        if (order == null)
        {
            invoice = await _uow.Invoices.GetAll()
                .Include(i => i.Order)
                    .ThenInclude(o => o.Table)
                        .ThenInclude(t => t.Area)
                .Include(i => i.Order)
                    .ThenInclude(o => o.Booking)
                .Include(i => i.Customer)
                .FirstOrDefaultAsync(i => i.Id == dto.InvoiceId, cancellationToken);

            if (invoice != null) order = invoice.Order;
        }

        if (order == null)
            return Result<InvoiceDto>.Failure(Error.NotFound("Order.NotFound", "Không tìm thấy đơn gọi món hoặc hóa đơn."));

        if (order.ClosedAt != null)
            return Result<InvoiceDto>.Failure(Error.Validation("Order.Closed", "Đơn gọi món đã thanh toán đóng bàn trước đó."));

        var cashier = await _uow.Staffs.GetByIdAsync(dto.CashierStaffId, cancellationToken);
        if (cashier == null)
            return Result<InvoiceDto>.Failure(Error.NotFound("Staff.NotFound", "Không tìm thấy nhân viên thu ngân."));

        var activeDetails = order.OrderDetails.Where(d => d.Status != OrderItemStatus.Cancelled).ToList();
        var itemsTotal = activeDetails.Sum(d => d.SubTotal);
        var customer = order.Booking?.Customer;
        var rank = customer?.Rank ?? MembershipRank.Standard;
        var deposit = order.Booking?.DepositAmount ?? 0m;

        var breakdown = _billingCalculator.Calculate(itemsTotal, order.CreatedAt, rank, 0, deposit);

        if (dto.PaymentMethod == PaymentMethod.Cash && dto.CashReceived < breakdown.FinalAmount)
        {
            return Result<InvoiceDto>.Failure(Error.Validation(
                "Payment.InsufficientCash",
                $"Số tiền khách đưa ({dto.CashReceived:N0}đ) không đủ để thanh toán hóa đơn ({breakdown.FinalAmount:N0}đ)."));
        }

        return await _uow.ExecuteInTransactionAsync(async () =>
        {
            var nowUtc = DateTime.UtcNow;
            var invoiceCode = $"INV-{nowUtc:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpper()}";

            var cashReceived = dto.PaymentMethod == PaymentMethod.Cash ? dto.CashReceived : breakdown.FinalAmount;
            var changeAmount = dto.PaymentMethod == PaymentMethod.Cash ? (dto.CashReceived - breakdown.FinalAmount) : 0m;

            var newInvoice = new Invoice
            {
                InvoiceCode = invoiceCode,
                OrderId = order.Id,
                BookingId = order.BookingId,
                CustomerId = customer?.Id,
                StaffId = dto.CashierStaffId,
                TotalAmount = breakdown.ItemsTotal,
                SurchargeAmount = breakdown.SurchargeAmount,
                VatAmount = breakdown.VatAmount,
                DiscountAmount = breakdown.DiscountAmount,
                DepositDeducted = breakdown.DepositDeducted,
                FinalAmount = breakdown.FinalAmount,
                PaymentMethod = dto.PaymentMethod,
                CashReceived = cashReceived,
                ChangeAmount = changeAmount,
                Status = InvoiceStatus.Paid,
                CreatedAt = nowUtc
            };

            await _uow.Invoices.AddAsync(newInvoice, cancellationToken);

            // Đóng đơn gọi món
            order.ClosedAt = nowUtc;
            order.Status = TableStatus.Cleaning;

            // Đổi trạng thái bàn sang Dọn dẹp
            order.Table.Status = TableStatus.Cleaning;

            // Đóng booking nếu có
            if (order.Booking != null)
            {
                order.Booking.Status = BookingStatus.Completed;
            }

            await _uow.SaveChangesAsync(cancellationToken);

            return Result<InvoiceDto>.Success(new InvoiceDto(
                newInvoice.Id,
                newInvoice.InvoiceCode,
                order.Id,
                order.OrderCode,
                order.Table.TableNumber,
                order.Table.Area.AreaName,
                customer?.CustomerName ?? order.Booking?.CustomerName,
                customer?.PhoneNumber ?? order.Booking?.PhoneNumber,
                rank,
                newInvoice.TotalAmount,
                newInvoice.SurchargeAmount,
                newInvoice.VatAmount,
                newInvoice.DiscountAmount,
                0,
                newInvoice.DepositDeducted,
                newInvoice.FinalAmount,
                newInvoice.PaymentMethod,
                newInvoice.Status,
                newInvoice.CreatedAt,
                newInvoice.CreatedAt,
                newInvoice.CashReceived,
                newInvoice.ChangeAmount,
                null,
                cashier.FullName
            ));
        }, cancellationToken);
    }

    public async Task<Result<InvoiceDto>> GetInvoiceByIdAsync(int invoiceId, CancellationToken cancellationToken = default)
    {
        var invoice = await _uow.Invoices.GetAll()
            .Include(i => i.Order)
                .ThenInclude(o => o.Table)
                    .ThenInclude(t => t.Area)
            .Include(i => i.Order)
                .ThenInclude(o => o.Booking)
            .Include(i => i.Customer)
            .Include(i => i.Staff)
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == invoiceId, cancellationToken);

        if (invoice == null)
            return Result<InvoiceDto>.Failure(Error.NotFound("Invoice.NotFound", "Không tìm thấy hóa đơn."));

        return Result<InvoiceDto>.Success(new InvoiceDto(
            invoice.Id,
            invoice.InvoiceCode,
            invoice.OrderId,
            invoice.Order.OrderCode,
            invoice.Order.Table.TableNumber,
            invoice.Order.Table.Area.AreaName,
            invoice.Customer?.CustomerName ?? invoice.Order.Booking?.CustomerName,
            invoice.Customer?.PhoneNumber ?? invoice.Order.Booking?.PhoneNumber,
            invoice.Customer?.Rank ?? MembershipRank.Standard,
            invoice.TotalAmount,
            invoice.SurchargeAmount,
            invoice.VatAmount,
            invoice.DiscountAmount,
            invoice.ManualDiscountAmount,
            invoice.DepositDeducted,
            invoice.FinalAmount,
            invoice.PaymentMethod,
            invoice.Status,
            invoice.CreatedAt,
            invoice.CreatedAt,
            invoice.CashReceived,
            invoice.ChangeAmount,
            null,
            invoice.Staff?.FullName ?? "Thu ngân"
        ));
    }

    public async Task<Result<InvoiceDto>> GetInvoiceByOrderIdAsync(int orderId, CancellationToken cancellationToken = default)
    {
        var invoice = await _uow.Invoices.GetAll()
            .Where(i => i.OrderId == orderId && i.Status == InvoiceStatus.Paid)
            .OrderByDescending(i => i.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (invoice == null)
            return Result<InvoiceDto>.Failure(Error.NotFound("Invoice.NotFound", "Chưa có hóa đơn thanh toán cho đơn này."));

        return await GetInvoiceByIdAsync(invoice.Id, cancellationToken);
    }

    public async Task<Result> CancelInvoiceAsync(int invoiceId, string reason, string managerPin, CancellationToken cancellationToken = default)
    {
        var isPinValid = await ValidateManagerPinAsync(managerPin, cancellationToken);
        if (!isPinValid)
            return Result.Failure(Error.Validation("Auth.InvalidPin", "Cần mã PIN Quản lý hợp lệ để hủy hóa đơn."));

        var invoice = await _uow.Invoices.GetAll()
            .Include(i => i.Order)
            .FirstOrDefaultAsync(i => i.Id == invoiceId, cancellationToken);

        if (invoice == null)
            return Result.Failure(Error.NotFound("Invoice.NotFound", "Không tìm thấy hóa đơn."));

        return await _uow.ExecuteInTransactionAsync(async () =>
        {
            invoice.Status = InvoiceStatus.Voided;
            invoice.VoidReason = reason;
            invoice.VoidedAt = DateTime.UtcNow;

            if (invoice.Order != null)
            {
                invoice.Order.ClosedAt = null;
                invoice.Order.Status = TableStatus.Occupied;
            }

            await _uow.AuditLogs.AddAsync(new AuditLog
            {
                Action = "CancelInvoice",
                EntityName = "Invoices",
                EntityId = invoice.Id.ToString(),
                Details = $"Hủy hóa đơn {invoice.InvoiceCode}. Lý do: {reason}",
                CreatedAt = DateTime.UtcNow
            }, cancellationToken);

            await _uow.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }, cancellationToken);
    }

    private async Task<bool> ValidateManagerPinAsync(string? pin, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(pin)) return false;

        var managers = await _uow.Staffs.GetAll()
            .Where(s => s.Role == StaffRole.Manager && s.IsActive && s.ApprovalPinHash != null)
            .ToListAsync(cancellationToken);

        foreach (var mgr in managers)
        {
            var verify = _passwordHasher.VerifyHashedPassword(mgr, mgr.ApprovalPinHash!, pin);
            if (verify == PasswordVerificationResult.Success)
                return true;
        }

        return false;
    }
}
