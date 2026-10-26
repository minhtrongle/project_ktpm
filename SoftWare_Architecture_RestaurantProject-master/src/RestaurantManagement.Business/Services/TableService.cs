using Microsoft.EntityFrameworkCore;
using RestaurantManagement.Business.DTOs;
using RestaurantManagement.Common.Enums;
using RestaurantManagement.Common.Results;
using RestaurantManagement.DataAccess.Entities;
using RestaurantManagement.DataAccess.UnitOfWork;

namespace RestaurantManagement.Business.Services;

public interface ITableService
{
    Task<List<AreaWithTablesDto>> GetFloorPlanAsync(CancellationToken cancellationToken = default);
    Task<Result<TableDto>> GetTableByIdAsync(int tableId, CancellationToken cancellationToken = default);
    Task<Result<OrderDto>> SeatWalkInAsync(SeatWalkInDto dto, CancellationToken cancellationToken = default);
    Task<Result<OrderDto>> CheckInBookingAsync(CheckInBookingDto dto, CancellationToken cancellationToken = default);
    Task<Result> CompleteCleaningAsync(int tableId, CancellationToken cancellationToken = default);
    Task<Result> UpdateTableStatusAsync(int tableId, TableStatus newStatus, CancellationToken cancellationToken = default);
}

public class TableService : ITableService
{
    private readonly IUnitOfWork _uow;

    public TableService(IUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task<List<AreaWithTablesDto>> GetFloorPlanAsync(CancellationToken cancellationToken = default)
    {
        var areas = await _uow.Areas.GetAll()
            .OrderBy(a => a.DisplayOrder)
            .Include(a => a.Tables)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        // Lấy thông tin order đang active (chưa đóng) cho các bàn
        var activeOrders = await _uow.Orders.GetAll()
            .Where(o => o.ClosedAt == null)
            .OrderByDescending(o => o.CreatedAt)
            .ThenByDescending(o => o.Id)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        var activeOrdersByTable = activeOrders
            .GroupBy(o => o.TableId)
            .ToDictionary(group => group.Key, group => group.First());

        // Lấy thông tin booking hôm nay
        var today = DateTime.UtcNow.Date;
        var activeBookings = await _uow.Bookings.GetAll()
            .Where(b => b.Status == BookingStatus.Confirmed && b.StartTime >= today && b.StartTime < today.AddDays(1))
            .OrderBy(b => b.StartTime)
            .ThenBy(b => b.Id)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        var activeBookingsByTable = activeBookings
            .GroupBy(b => b.TableId)
            .ToDictionary(group => group.Key, group => group.First().CustomerName);

        return areas.Select(area => new AreaWithTablesDto(
            area.Id,
            area.AreaName,
            area.Description,
            area.Tables.OrderBy(t => t.TableNumber).Select(table =>
            {
                activeOrdersByTable.TryGetValue(table.Id, out var ord);
                activeBookingsByTable.TryGetValue(table.Id, out var customerName);

                return new TableDto(
                    table.Id,
                    table.TableNumber,
                    table.AreaId,
                    area.AreaName,
                    table.Capacity,
                    table.Status,
                    ord?.OrderCode,
                    ord?.GuestCount,
                    customerName
                );
            }).ToList()
        )).ToList();
    }

    public async Task<Result<TableDto>> GetTableByIdAsync(int tableId, CancellationToken cancellationToken = default)
    {
        var table = await _uow.Tables.GetAll()
            .Include(t => t.Area)
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == tableId, cancellationToken);

        if (table == null)
            return Result<TableDto>.Failure(Error.NotFound("Table.NotFound", $"Không tìm thấy bàn #{tableId}."));

        var activeOrder = await _uow.Orders.GetAll()
            .Where(o => o.TableId == tableId && o.ClosedAt == null)
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);

        return Result<TableDto>.Success(new TableDto(
            table.Id,
            table.TableNumber,
            table.AreaId,
            table.Area.AreaName,
            table.Capacity,
            table.Status,
            activeOrder?.OrderCode,
            activeOrder?.GuestCount
        ));
    }

    public async Task<Result<OrderDto>> SeatWalkInAsync(SeatWalkInDto dto, CancellationToken cancellationToken = default)
    {
        var table = await _uow.Tables.GetByIdAsync(dto.TableId, cancellationToken);
        if (table == null)
            return Result<OrderDto>.Failure(Error.NotFound("Table.NotFound", "Không tìm thấy bàn yêu cầu."));

        if (table.Status != TableStatus.Available)
            return Result<OrderDto>.Failure(Error.Validation("Table.NotAvailable", $"Bàn {table.TableNumber} hiện không khả dụng (Trạng thái: {table.Status})."));

        if (dto.GuestCount > table.Capacity)
            return Result<OrderDto>.Failure(Error.Validation("Table.OverCapacity", $"Số lượng khách ({dto.GuestCount}) vượt quá sức chứa bàn {table.TableNumber} ({table.Capacity} chỗ)."));

        var staff = await _uow.Staffs.GetByIdAsync(dto.StaffId, cancellationToken);
        if (staff == null)
            return Result<OrderDto>.Failure(Error.NotFound("Staff.NotFound", "Không tìm thấy nhân viên tiếp đón."));

        return await _uow.ExecuteInTransactionAsync(async () =>
        {
            table.Status = TableStatus.Occupied;

            var orderCode = $"ORD-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpper()}";
            var order = new Order
            {
                OrderCode = orderCode,
                TableId = table.Id,
                CreatedById = dto.StaffId,
                GuestCount = dto.GuestCount,
                Status = TableStatus.Occupied,
                CreatedAt = DateTime.UtcNow
            };

            await _uow.Orders.AddAsync(order, cancellationToken);
            await _uow.SaveChangesAsync(cancellationToken);

            var area = await _uow.Areas.GetByIdAsync(table.AreaId, cancellationToken);

            return Result<OrderDto>.Success(new OrderDto(
                order.Id,
                order.OrderCode,
                table.Id,
                table.TableNumber,
                area?.AreaName ?? string.Empty,
                order.GuestCount,
                table.Status,
                false,
                order.CreatedAt,
                staff.FullName,
                null,
                0,
                new List<OrderItemDto>(),
                0
            ));
        }, cancellationToken);
    }

    public async Task<Result<OrderDto>> CheckInBookingAsync(CheckInBookingDto dto, CancellationToken cancellationToken = default)
    {
        var booking = await _uow.Bookings.GetAll()
            .Include(b => b.Table)
            .FirstOrDefaultAsync(b => b.Id == dto.BookingId, cancellationToken);

        if (booking == null)
            return Result<OrderDto>.Failure(Error.NotFound("Booking.NotFound", "Không tìm thấy đơn đặt bàn."));

        if (booking.Status != BookingStatus.Confirmed)
            return Result<OrderDto>.Failure(Error.Validation("Booking.InvalidStatus", $"Đơn đặt bàn {booking.BookingCode} không ở trạng thái 'Đã xác nhận' (hiện tại: {booking.Status})."));

        var table = booking.Table;
        var staff = await _uow.Staffs.GetByIdAsync(dto.StaffId, cancellationToken);
        if (staff == null)
            return Result<OrderDto>.Failure(Error.NotFound("Staff.NotFound", "Không tìm thấy nhân viên tiếp đón."));

        return await _uow.ExecuteInTransactionAsync(async () =>
        {
            booking.Status = BookingStatus.CheckedIn;
            booking.CheckedInAt = DateTime.UtcNow;
            table.Status = TableStatus.Occupied;

            var orderCode = $"ORD-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpper()}";
            var order = new Order
            {
                OrderCode = orderCode,
                TableId = table.Id,
                BookingId = booking.Id,
                CreatedById = dto.StaffId,
                GuestCount = booking.GuestCount,
                Status = TableStatus.Occupied,
                CreatedAt = DateTime.UtcNow
            };

            await _uow.Orders.AddAsync(order, cancellationToken);
            await _uow.SaveChangesAsync(cancellationToken);

            var area = await _uow.Areas.GetByIdAsync(table.AreaId, cancellationToken);

            return Result<OrderDto>.Success(new OrderDto(
                order.Id,
                order.OrderCode,
                table.Id,
                table.TableNumber,
                area?.AreaName ?? string.Empty,
                order.GuestCount,
                table.Status,
                false,
                order.CreatedAt,
                staff.FullName,
                booking.Id,
                booking.DepositAmount,
                new List<OrderItemDto>(),
                0
            ));
        }, cancellationToken);
    }

    public async Task<Result> CompleteCleaningAsync(int tableId, CancellationToken cancellationToken = default)
    {
        var table = await _uow.Tables.GetByIdAsync(tableId, cancellationToken);
        if (table == null)
            return Result.Failure(Error.NotFound("Table.NotFound", "Không tìm thấy bàn."));

        if (table.Status != TableStatus.Cleaning)
            return Result.Failure(Error.Validation("Table.NotCleaning", $"Bàn {table.TableNumber} không ở trạng thái 'Đang dọn dẹp'."));

        table.Status = TableStatus.Available;
        await _uow.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> UpdateTableStatusAsync(int tableId, TableStatus newStatus, CancellationToken cancellationToken = default)
    {
        var table = await _uow.Tables.GetByIdAsync(tableId, cancellationToken);
        if (table == null)
            return Result.Failure(Error.NotFound("Table.NotFound", "Không tìm thấy bàn."));

        table.Status = newStatus;
        await _uow.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
