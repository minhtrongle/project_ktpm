using Microsoft.EntityFrameworkCore;
using RestaurantManagement.Common.Enums;
using RestaurantManagement.DataAccess.Context;
using RestaurantManagement.DataAccess.Entities;

namespace RestaurantManagement.DataAccess.Repositories;

public interface IBookingRepository : IRepository<Booking>
{
    Task<bool> HasOverlapAsync(int tableId, DateTime startTime, DateTime endTime, DateTime nowUtc, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Booking>> GetActiveBookingsByDateAsync(DateOnly date, CancellationToken cancellationToken = default);
    Task<Booking?> GetByCodeAsync(string bookingCode, CancellationToken cancellationToken = default);
}

public class BookingRepository : Repository<Booking>, IBookingRepository
{
    public BookingRepository(RestaurantDbContext context) : base(context)
    {
    }

    public async Task<bool> HasOverlapAsync(int tableId, DateTime startTime, DateTime endTime, DateTime nowUtc, CancellationToken cancellationToken = default)
    {
        // Chồng lấn khi: (b.StartTime < endTime) && (b.EndTime > startTime)
        // và booking đang PendingPayment (chưa hết 15') hoặc Confirmed hoặc CheckedIn
        return await _context.Bookings
            .AnyAsync(b => b.TableId == tableId &&
                           ((b.Status == BookingStatus.Confirmed || b.Status == BookingStatus.CheckedIn) ||
                            (b.Status == BookingStatus.PendingPayment && b.HoldExpiresAt > nowUtc)) &&
                           b.StartTime < endTime && b.EndTime > startTime,
                      cancellationToken);
    }

    public async Task<IReadOnlyList<Booking>> GetActiveBookingsByDateAsync(DateOnly date, CancellationToken cancellationToken = default)
    {
        var startOfDay = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var endOfDay = date.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);

        return await _context.Bookings
            .AsNoTracking()
            .Include(b => b.Table)
            .Include(b => b.Customer)
            .Where(b => b.StartTime >= startOfDay && b.StartTime <= endOfDay &&
                        (b.Status == BookingStatus.Confirmed || b.Status == BookingStatus.CheckedIn || b.Status == BookingStatus.PendingPayment))
            .OrderBy(b => b.StartTime)
            .ToListAsync(cancellationToken);
    }

    public async Task<Booking?> GetByCodeAsync(string bookingCode, CancellationToken cancellationToken = default)
    {
        return await _context.Bookings
            .Include(b => b.Table)
                .ThenInclude(t => t.Area)
            .Include(b => b.Customer)
            .Include(b => b.PaymentTransactions)
            .SingleOrDefaultAsync(b => b.BookingCode == bookingCode, cancellationToken);
    }
}
