using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RestaurantManagement.Business.DTOs;
using RestaurantManagement.Business.Policies;
using RestaurantManagement.Common.Enums;
using RestaurantManagement.Common.Results;
using RestaurantManagement.DataAccess.Entities;
using RestaurantManagement.DataAccess.UnitOfWork;

namespace RestaurantManagement.Business.Services;

public interface IOrderService
{
    Task<Result<OrderDto>> GetActiveOrderByTableIdAsync(int tableId, CancellationToken cancellationToken = default);
    Task<Result<OrderDto>> GetOrderByIdAsync(int orderId, CancellationToken cancellationToken = default);
    Task<Result<OrderItemDto>> AddOrderItemAsync(AddOrderItemDto dto, CancellationToken cancellationToken = default);
    Task<Result> UpdateOrderItemQuantityAsync(int orderDetailId, int quantity, string? note, CancellationToken cancellationToken = default);
    Task<Result<SendKotResultDto>> SendToKitchenAsync(int orderId, CancellationToken cancellationToken = default);
    Task<Result> ChangeOrderItemAsync(ChangeOrderItemDto dto, CancellationToken cancellationToken = default);
    Task<Result> ReturnOrderItemAsync(ReturnOrderItemDto dto, CancellationToken cancellationToken = default);
    Task<Result<PreBillDto>> GetPreBillAsync(int orderId, CancellationToken cancellationToken = default);
    Task<Result> RequestPaymentAsync(int orderId, CancellationToken cancellationToken = default);
    Task<List<OrderDto>> GetActiveOrdersAsync(CancellationToken cancellationToken = default);
    Task<Result> UpdateItemStatusAsync(int orderDetailId, OrderItemStatus newStatus, CancellationToken cancellationToken = default);
}

public class OrderService : IOrderService
{
    private readonly IUnitOfWork _uow;
    private readonly BillingCalculator _billingCalculator;
    private readonly IPasswordHasher<Staff> _passwordHasher;

    public OrderService(
        IUnitOfWork uow,
        BillingCalculator billingCalculator,
        IPasswordHasher<Staff> passwordHasher)
    {
        _uow = uow;
        _billingCalculator = billingCalculator;
        _passwordHasher = passwordHasher;
    }

    public async Task<Result<OrderDto>> GetActiveOrderByTableIdAsync(int tableId, CancellationToken cancellationToken = default)
    {
        var order = await _uow.Orders.GetAll()
            .Include(o => o.Table)
                .ThenInclude(t => t.Area)
            .Include(o => o.CreatedBy)
            .Include(o => o.Booking)
            .Include(o => o.OrderDetails)
                .ThenInclude(d => d.MenuItem)
            .Where(o => o.TableId == tableId && o.ClosedAt == null)
            .OrderByDescending(o => o.CreatedAt)
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);

        if (order == null)
            return Result<OrderDto>.Failure(Error.NotFound("Order.NotFound", $"Bàn #{tableId} hiện chưa có đơn gọi món đang hoạt động."));

        return Result<OrderDto>.Success(MapToDto(order));
    }

    public async Task<Result<OrderDto>> GetOrderByIdAsync(int orderId, CancellationToken cancellationToken = default)
    {
        var order = await _uow.Orders.GetAll()
            .Include(o => o.Table)
                .ThenInclude(t => t.Area)
            .Include(o => o.CreatedBy)
            .Include(o => o.Booking)
            .Include(o => o.OrderDetails)
                .ThenInclude(d => d.MenuItem)
            .Where(o => o.Id == orderId)
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);

        if (order == null)
            return Result<OrderDto>.Failure(Error.NotFound("Order.NotFound", $"Không tìm thấy đơn gọi món #{orderId}."));

        return Result<OrderDto>.Success(MapToDto(order));
    }

    public async Task<Result<OrderItemDto>> AddOrderItemAsync(AddOrderItemDto dto, CancellationToken cancellationToken = default)
    {
        if (dto.Quantity <= 0)
            return Result<OrderItemDto>.Failure(Error.Validation("Item.InvalidQuantity", "Số lượng món phải lớn hơn 0."));

        var order = await _uow.Orders.GetAll()
            .Include(o => o.OrderDetails)
            .FirstOrDefaultAsync(o => o.Id == dto.OrderId, cancellationToken);

        if (order == null)
            return Result<OrderItemDto>.Failure(Error.NotFound("Order.NotFound", "Đơn gọi món không tồn tại."));

        if (order.ClosedAt != null)
            return Result<OrderItemDto>.Failure(Error.Validation("Order.Closed", "Đơn gọi món đã đóng hoặc hoàn tất, không thể thêm món."));

        var menuItem = await _uow.MenuItems.GetByIdAsync(dto.MenuItemId, cancellationToken);
        if (menuItem == null)
            return Result<OrderItemDto>.Failure(Error.NotFound("MenuItem.NotFound", "Món ăn không tồn tại."));

        if (!menuItem.IsAvailable)
            return Result<OrderItemDto>.Failure(Error.Validation("MenuItem.Unavailable", $"Món '{menuItem.ItemName}' tạm ngưng phục vụ hoặc hết nguyên liệu."));

        var existingItem = order.OrderDetails
            .FirstOrDefault(d => d.MenuItemId == dto.MenuItemId && d.Status == OrderItemStatus.Pending && d.Note == dto.Note);

        OrderDetail detail;
        if (existingItem != null)
        {
            existingItem.Quantity += dto.Quantity;
            existingItem.SubTotal = existingItem.Quantity * existingItem.UnitPrice;
            detail = existingItem;
        }
        else
        {
            detail = new OrderDetail
            {
                OrderId = order.Id,
                MenuItemId = menuItem.Id,
                Quantity = dto.Quantity,
                UnitPrice = menuItem.Price,
                SubTotal = dto.Quantity * menuItem.Price,
                Status = OrderItemStatus.Pending,
                Note = dto.Note,
                CreatedAt = DateTime.UtcNow
            };
            order.OrderDetails.Add(detail);
        }

        order.TotalAmount = order.OrderDetails
            .Where(d => d.Status != OrderItemStatus.Cancelled)
            .Sum(d => d.SubTotal);

        await _uow.SaveChangesAsync(cancellationToken);

        return Result<OrderItemDto>.Success(new OrderItemDto(
            detail.Id,
            menuItem.Id,
            menuItem.ItemCode,
            menuItem.ItemName,
            menuItem.Unit,
            detail.Quantity,
            detail.UnitPrice,
            detail.SubTotal,
            detail.Status,
            menuItem.KitchenStation,
            detail.Note,
            detail.CreatedAt
        ));
    }

    public async Task<Result> UpdateOrderItemQuantityAsync(int orderDetailId, int quantity, string? note, CancellationToken cancellationToken = default)
    {
        var detail = await _uow.OrderDetails.GetAll()
            .Include(d => d.Order)
            .FirstOrDefaultAsync(d => d.Id == orderDetailId, cancellationToken);

        if (detail == null)
            return Result.Failure(Error.NotFound("OrderDetail.NotFound", "Không tìm thấy món ăn trong đơn."));

        if (detail.Order.ClosedAt != null)
            return Result.Failure(Error.Validation("Order.Closed", "Đơn gọi món đã đóng."));

        if (detail.Status != OrderItemStatus.Pending)
            return Result.Failure(Error.Validation("OrderDetail.AlreadySent", "Món ăn đã được gửi xuống bếp, không thể sửa trực tiếp. Vui lòng chọn chức năng 'Đổi món' hoặc 'Trả món'."));

        if (quantity <= 0)
        {
            _uow.OrderDetails.Remove(detail);
        }
        else
        {
            detail.Quantity = quantity;
            detail.SubTotal = detail.Quantity * detail.UnitPrice;
            if (note != null) detail.Note = note;
        }

        await _uow.SaveChangesAsync(cancellationToken);

        detail.Order.TotalAmount = await _uow.OrderDetails.GetAll()
            .Where(d => d.OrderId == detail.OrderId && d.Status != OrderItemStatus.Cancelled)
            .SumAsync(d => d.SubTotal, cancellationToken);

        await _uow.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result<SendKotResultDto>> SendToKitchenAsync(int orderId, CancellationToken cancellationToken = default)
    {
        var order = await _uow.Orders.GetAll()
            .Include(o => o.Table)
            .Include(o => o.OrderDetails)
                .ThenInclude(d => d.MenuItem)
            .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);

        if (order == null)
            return Result<SendKotResultDto>.Failure(Error.NotFound("Order.NotFound", "Đơn gọi món không tồn tại."));

        var pendingItems = order.OrderDetails
            .Where(d => d.Status == OrderItemStatus.Pending)
            .ToList();

        if (pendingItems.Count == 0)
            return Result<SendKotResultDto>.Failure(Error.Validation("Order.NoNewItems", "Không có món mới nào cần gửi bếp."));

        var ticketCodes = new List<string>();
        var nowUtc = DateTime.UtcNow;

        return await _uow.ExecuteInTransactionAsync(async () =>
        {
            var groups = pendingItems.GroupBy(i => i.MenuItem.KitchenStation);

            foreach (var group in groups)
            {
                var station = group.Key;
                var ticketCode = $"KOT-{station}-{nowUtc:HHmmss}-{Guid.NewGuid().ToString("N")[..4].ToUpper()}";
                ticketCodes.Add(ticketCode);

                var itemsPayload = group.Select(i => new
                {
                    ItemName = i.MenuItem.ItemName,
                    Quantity = i.Quantity,
                    Note = i.Note
                });

                var kot = new KotTicket
                {
                    TicketCode = ticketCode,
                    OrderId = order.Id,
                    Station = station,
                    Type = KotTicketType.New,
                    PayloadJson = JsonSerializer.Serialize(new
                    {
                        TableNumber = order.Table.TableNumber,
                        SentAt = nowUtc,
                        Items = itemsPayload
                    }),
                    Status = KotTicketStatus.Queued,
                    CreatedAt = nowUtc,
                    RequestId = Guid.NewGuid().ToString()
                };

                await _uow.KotTickets.AddAsync(kot, cancellationToken);

                foreach (var item in group)
                {
                    item.Status = OrderItemStatus.Cooking;
                    item.SentToKitchenAt = nowUtc;
                }
            }

            await _uow.SaveChangesAsync(cancellationToken);

            return Result<SendKotResultDto>.Success(new SendKotResultDto(
                order.Id,
                ticketCodes,
                pendingItems.Count,
                nowUtc
            ));
        }, cancellationToken);
    }

    public async Task<Result> ChangeOrderItemAsync(ChangeOrderItemDto dto, CancellationToken cancellationToken = default)
    {
        var detail = await _uow.OrderDetails.GetAll()
            .Include(d => d.Order)
                .ThenInclude(o => o.Table)
            .Include(d => d.MenuItem)
            .FirstOrDefaultAsync(d => d.Id == dto.OrderDetailId, cancellationToken);

        if (detail == null)
            return Result.Failure(Error.NotFound("OrderDetail.NotFound", "Không tìm thấy món cần đổi."));

        if (detail.Status == OrderItemStatus.Served || detail.Status == OrderItemStatus.Cancelled)
            return Result.Failure(Error.Validation("OrderDetail.InvalidStatus", $"Món ăn đang ở trạng thái {detail.Status}, không thể đổi."));

        if (detail.Status == OrderItemStatus.Ready || detail.Status == OrderItemStatus.Cooking)
        {
            var isPinValid = await ValidateManagerPinAsync(dto.ManagerPin, cancellationToken);
            if (!isPinValid)
                return Result.Failure(Error.Validation("Auth.InvalidPin", "Món ăn đã được gửi chế biến. Cần mã PIN Quản lý để phê duyệt đổi món."));
        }

        var newMenuItem = await _uow.MenuItems.GetByIdAsync(dto.NewMenuItemId, cancellationToken);
        if (newMenuItem == null)
            return Result.Failure(Error.NotFound("MenuItem.NotFound", "Món mới không tồn tại."));

        return await _uow.ExecuteInTransactionAsync(async () =>
        {
            detail.Status = OrderItemStatus.Cancelled;
            detail.CancelReason = dto.Reason;
            detail.Note = $"{detail.Note} [ĐÃ ĐỔI: {dto.Reason}]";

            var newDetail = new OrderDetail
            {
                OrderId = detail.OrderId,
                MenuItemId = newMenuItem.Id,
                Quantity = dto.NewQuantity,
                UnitPrice = newMenuItem.Price,
                SubTotal = dto.NewQuantity * newMenuItem.Price,
                Status = OrderItemStatus.Pending,
                Note = $"[Đổi từ {detail.MenuItem.ItemName}]",
                CreatedAt = DateTime.UtcNow
            };
            await _uow.OrderDetails.AddAsync(newDetail, cancellationToken);

            detail.Order.TotalAmount = await _uow.OrderDetails.GetAll()
                .Where(d => d.OrderId == detail.OrderId && d.Id != detail.Id && d.Status != OrderItemStatus.Cancelled)
                .SumAsync(d => d.SubTotal, cancellationToken) + newDetail.SubTotal;

            await _uow.AuditLogs.AddAsync(new AuditLog
            {
                Action = "ChangeOrderItem",
                EntityName = "OrderDetails",
                EntityId = detail.Id.ToString(),
                Details = $"Đổi từ {detail.MenuItem.ItemName} sang {newMenuItem.ItemName} x{dto.NewQuantity}. Lý do: {dto.Reason}",
                CreatedAt = DateTime.UtcNow
            }, cancellationToken);

            await _uow.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }, cancellationToken);
    }

    public async Task<Result> ReturnOrderItemAsync(ReturnOrderItemDto dto, CancellationToken cancellationToken = default)
    {
        var detail = await _uow.OrderDetails.GetAll()
            .Include(d => d.Order)
            .Include(d => d.MenuItem)
            .FirstOrDefaultAsync(d => d.Id == dto.OrderDetailId, cancellationToken);

        if (detail == null)
            return Result.Failure(Error.NotFound("OrderDetail.NotFound", "Không tìm thấy món cần trả."));

        if (detail.Status == OrderItemStatus.Cancelled || detail.Status == OrderItemStatus.Returned)
            return Result.Failure(Error.Validation("OrderDetail.AlreadyCancelled", "Món này đã bị hủy hoặc trả trước đó."));

        var isPinValid = await ValidateManagerPinAsync(dto.ManagerPin, cancellationToken);
        if (!isPinValid)
            return Result.Failure(Error.Validation("Auth.InvalidPin", "Cần mã PIN Quản lý để phê duyệt trả món."));

        return await _uow.ExecuteInTransactionAsync(async () =>
        {
            detail.Status = OrderItemStatus.Returned;
            detail.ReturnReason = dto.Reason;
            detail.ReturnType = dto.RestaurantFault ? "RestaurantFault" : "CustomerMindChange";

            if (dto.RestaurantFault)
            {
                detail.UnitPrice = 0;
                detail.SubTotal = 0;
                detail.Note = $"{detail.Note} [TRẢ MÓN - LỖI NHÀ HÀNG: {dto.Reason}]";
            }
            else
            {
                detail.Note = $"{detail.Note} [TRẢ MÓN - KHÁCH ĐỔI Ý: {dto.Reason}]";
            }

            detail.Order.TotalAmount = await _uow.OrderDetails.GetAll()
                .Where(d => d.OrderId == detail.OrderId && d.Id != detail.Id && d.Status != OrderItemStatus.Cancelled)
                .SumAsync(d => d.SubTotal, cancellationToken) + (dto.RestaurantFault ? 0 : detail.SubTotal);

            await _uow.AuditLogs.AddAsync(new AuditLog
            {
                Action = "ReturnOrderItem",
                EntityName = "OrderDetails",
                EntityId = detail.Id.ToString(),
                Details = $"Trả món: {detail.MenuItem.ItemName}. Lỗi nhà hàng: {dto.RestaurantFault}. Lý do: {dto.Reason}",
                CreatedAt = DateTime.UtcNow
            }, cancellationToken);

            await _uow.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }, cancellationToken);
    }

    public async Task<Result<PreBillDto>> GetPreBillAsync(int orderId, CancellationToken cancellationToken = default)
    {
        var order = await _uow.Orders.GetAll()
            .Include(o => o.Table)
                .ThenInclude(t => t.Area)
            .Include(o => o.Booking)
            .Include(o => o.OrderDetails)
                .ThenInclude(d => d.MenuItem)
            .Where(o => o.Id == orderId)
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);

        if (order == null)
            return Result<PreBillDto>.Failure(Error.NotFound("Order.NotFound", "Không tìm thấy đơn gọi món."));

        var items = order.OrderDetails
            .Where(d => d.Status != OrderItemStatus.Cancelled)
            .Select(d => new OrderItemDto(
                d.Id,
                d.MenuItemId,
                d.MenuItem.ItemCode,
                d.MenuItem.ItemName,
                d.MenuItem.Unit,
                d.Quantity,
                d.UnitPrice,
                d.SubTotal,
                d.Status,
                d.MenuItem.KitchenStation,
                d.Note,
                d.CreatedAt
            )).ToList();

        var itemsTotal = items.Sum(i => i.SubTotal);
        var deposit = order.Booking?.DepositAmount ?? 0m;

        var vatAmount = BillingCalculator.Round(itemsTotal * 0.08m);
        var subTotalBeforeDeposit = itemsTotal + vatAmount;
        var depositDeducted = Math.Min(deposit, subTotalBeforeDeposit);
        var finalAmount = Math.Max(0, subTotalBeforeDeposit - depositDeducted);

        return Result<PreBillDto>.Success(new PreBillDto(
            order.OrderCode,
            order.Table.TableNumber,
            order.Table.Area.AreaName,
            order.GuestCount,
            order.CreatedAt,
            items,
            itemsTotal,
            0,
            vatAmount,
            0,
            depositDeducted,
            BillingCalculator.Round(finalAmount)
        ));
    }

    public async Task<Result> RequestPaymentAsync(int orderId, CancellationToken cancellationToken = default)
    {
        var order = await _uow.Orders.GetAll()
            .Include(o => o.Table)
            .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);

        if (order == null)
            return Result.Failure(Error.NotFound("Order.NotFound", "Không tìm thấy đơn gọi món."));

        order.Status = TableStatus.PreCheck;
        order.Table.Status = TableStatus.PreCheck;

        await _uow.SaveChangesAsync(cancellationToken);
        return Result.Success();
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

    public async Task<List<OrderDto>> GetActiveOrdersAsync(CancellationToken cancellationToken = default)
    {
        var orders = await _uow.Orders.GetAll()
            .Include(o => o.Table)
                .ThenInclude(t => t.Area)
            .Include(o => o.CreatedBy)
            .Include(o => o.Booking)
            .Include(o => o.OrderDetails)
                .ThenInclude(d => d.MenuItem)
            .Where(o => o.ClosedAt == null)
            .OrderBy(o => o.CreatedAt)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return orders.Select(MapToDto).ToList();
    }

    public async Task<Result> UpdateItemStatusAsync(int orderDetailId, OrderItemStatus newStatus, CancellationToken cancellationToken = default)
    {
        var detail = await _uow.OrderDetails.GetByIdAsync(orderDetailId, cancellationToken);
        if (detail == null)
            return Result.Failure(Error.NotFound("OrderDetail.NotFound", "Không tìm thấy món trong đơn."));

        detail.Status = newStatus;
        await _uow.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private static OrderDto MapToDto(Order order)
    {
        var items = order.OrderDetails.Select(d => new OrderItemDto(
            d.Id,
            d.MenuItemId,
            d.MenuItem?.ItemCode ?? string.Empty,
            d.MenuItem?.ItemName ?? string.Empty,
            d.MenuItem?.Unit,
            d.Quantity,
            d.UnitPrice,
            d.SubTotal,
            d.Status,
            d.MenuItem?.KitchenStation ?? KitchenStation.HotKitchen,
            d.Note,
            d.CreatedAt
        )).ToList();

        var total = items.Where(i => i.Status != OrderItemStatus.Cancelled).Sum(i => i.SubTotal);

        return new OrderDto(
            order.Id,
            order.OrderCode,
            order.TableId,
            order.Table?.TableNumber ?? string.Empty,
            order.Table?.Area?.AreaName ?? string.Empty,
            order.GuestCount,
            order.Table?.Status ?? TableStatus.Occupied,
            order.ClosedAt != null,
            order.CreatedAt,
            order.CreatedBy?.FullName ?? string.Empty,
            order.BookingId,
            order.Booking?.DepositAmount ?? 0,
            items,
            total
        );
    }
}
