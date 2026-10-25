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

public interface IBookingService
{
    Task<Result<List<AvailableTableDto>>> SearchAvailableTablesAsync(SearchAvailableTablesQueryDto query, CancellationToken cancellationToken = default);
    Task<Result<BookingConfirmationDto>> CreateBookingAsync(CreateBookingDto dto, CancellationToken cancellationToken = default);
    Task<Result<BookingConfirmationDto>> ConfirmBookingPaymentAsync(int bookingId, string transactionRef, string provider, CancellationToken cancellationToken = default);
    Task<Result<BookingConfirmationDto>> GetBookingByIdAsync(int bookingId, CancellationToken cancellationToken = default);
    Task<Result<BookingConfirmationDto>> GetBookingByCodeAsync(string bookingCode, string phoneNumber, CancellationToken cancellationToken = default);
    Task<Result<CancelBookingResultDto>> CancelBookingAsync(CancelBookingDto dto, CancellationToken cancellationToken = default);
    Task<List<BookingConfirmationDto>> GetTodayBookingsAsync(CancellationToken cancellationToken = default);
}

public class BookingService : IBookingService
{
    private readonly IUnitOfWork _uow;
    private readonly DepositPolicy _depositPolicy;
    private readonly RefundPolicy _refundPolicy;
    private readonly RestaurantRulesOptions _options;
    private readonly TimeZoneInfo _timeZone;

    public BookingService(
        IUnitOfWork uow,
        DepositPolicy depositPolicy,
        RefundPolicy refundPolicy,
        IOptions<RestaurantRulesOptions> options)
    {
        _uow = uow;
        _depositPolicy = depositPolicy;
        _refundPolicy = refundPolicy;
        _options = options.Value;

        try
        {
            _timeZone = TimeZoneInfo.FindSystemTimeZoneById(_options.TimeZone);
        }
        catch
        {
            _timeZone = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
        }
    }

    public async Task<Result<List<AvailableTableDto>>> SearchAvailableTablesAsync(SearchAvailableTablesQueryDto query, CancellationToken cancellationToken = default)
    {
        var localBookingDateTime = query.BookingDate.ToDateTime(query.BookingTime);
        var bookingDateTimeUtc = TimeZoneInfo.ConvertTimeToUtc(localBookingDateTime, _timeZone);
        var minBookingTimeUtc = DateTime.UtcNow.AddMinutes(_options.Booking.MinLeadMinutes);

        if (bookingDateTimeUtc < minBookingTimeUtc)
        {
            return Result<List<AvailableTableDto>>.Failure(Error.Validation(
                "Booking.MinLeadTime",
                $"Thời gian đặt bàn phải trước thời điểm hiện tại tối thiểu {_options.Booking.MinLeadMinutes} phút."));
        }

        if (query.GuestCount < 1)
        {
            return Result<List<AvailableTableDto>>.Failure(Error.Validation("Booking.InvalidGuests", "Số lượng khách phải từ 1 người trở lên."));
        }

        var tablesQuery = _uow.Tables.GetAll()
            .Include(t => t.Area)
            .Where(t => t.Capacity >= query.GuestCount);

        if (query.AreaId.HasValue && query.AreaId.Value > 0)
        {
            tablesQuery = tablesQuery.Where(t => t.AreaId == query.AreaId.Value);
        }

        var candidateTables = await tablesQuery.ToListAsync(cancellationToken);

        var slotHours = _options.Booking.SlotHours;
        var slotStartUtc = bookingDateTimeUtc.AddHours(-slotHours);
        var slotEndUtc = bookingDateTimeUtc.AddHours(slotHours);
        var nowUtc = DateTime.UtcNow;

        var conflictingBookings = await _uow.Bookings.GetAll()
            .Where(b => (b.Status == BookingStatus.Confirmed || (b.Status == BookingStatus.PendingPayment && b.HoldExpiresAt > nowUtc))
                     && b.StartTime >= slotStartUtc
                     && b.StartTime <= slotEndUtc)
            .Select(b => b.TableId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var availableTables = candidateTables
            .Where(t => !conflictingBookings.Contains(t.Id))
            .OrderBy(t => t.Area.DisplayOrder)
            .ThenBy(t => t.Capacity)
            .ThenBy(t => t.TableNumber)
            .Select(t => new AvailableTableDto(
                t.Id,
                t.TableNumber,
                t.AreaId,
                t.Area.AreaName,
                t.Capacity
            ))
            .ToList();

        return Result<List<AvailableTableDto>>.Success(availableTables);
    }

    public async Task<Result<BookingConfirmationDto>> CreateBookingAsync(CreateBookingDto dto, CancellationToken cancellationToken = default)
    {
        var localBookingDateTime = dto.BookingDate.ToDateTime(dto.BookingTime);
        var bookingDateTimeUtc = TimeZoneInfo.ConvertTimeToUtc(localBookingDateTime, _timeZone);
        var nowUtc = DateTime.UtcNow;

        if (bookingDateTimeUtc < nowUtc.AddMinutes(_options.Booking.MinLeadMinutes))
        {
            return Result<BookingConfirmationDto>.Failure(Error.Validation(
                "Booking.MinLeadTime",
                $"Thời gian đặt bàn phải trước thời điểm hiện tại tối thiểu {_options.Booking.MinLeadMinutes} phút."));
        }

        var table = await _uow.Tables.GetAll()
            .Include(t => t.Area)
            .FirstOrDefaultAsync(t => t.Id == dto.TableId, cancellationToken);

        if (table == null)
            return Result<BookingConfirmationDto>.Failure(Error.NotFound("Table.NotFound", "Bàn đã chọn không tồn tại."));

        if (dto.GuestCount > table.Capacity)
            return Result<BookingConfirmationDto>.Failure(Error.Validation("Table.OverCapacity", $"Số lượng khách ({dto.GuestCount}) vượt quá sức chứa bàn ({table.Capacity})."));

        var slotHours = _options.Booking.SlotHours;
        var slotStartUtc = bookingDateTimeUtc.AddHours(-slotHours);
        var slotEndUtc = bookingDateTimeUtc.AddHours(slotHours);

        var hasConflict = await _uow.Bookings.GetAll()
            .AnyAsync(b => b.TableId == table.Id
                        && (b.Status == BookingStatus.Confirmed || (b.Status == BookingStatus.PendingPayment && b.HoldExpiresAt > nowUtc))
                        && b.StartTime >= slotStartUtc
                        && b.StartTime <= slotEndUtc, cancellationToken);

        if (hasConflict)
        {
            return Result<BookingConfirmationDto>.Failure(Error.Conflict(
                "Booking.Conflict",
                $"Bàn {table.TableNumber} đã có người đặt trong khung giờ này. Vui lòng chọn bàn khác hoặc khung giờ khác."));
        }

        var depositAmount = _depositPolicy.CalculateDeposit(dto.GuestCount);

        return await _uow.ExecuteInTransactionAsync(async () =>
        {
            var customer = await _uow.Customers.GetAll()
                .FirstOrDefaultAsync(c => c.PhoneNumber == dto.PhoneNumber, cancellationToken);

            if (customer == null)
            {
                customer = new Customer
                {
                    CustomerName = dto.CustomerName,
                    PhoneNumber = dto.PhoneNumber,
                    Email = dto.Email,
                    CreatedAt = nowUtc
                };
                await _uow.Customers.AddAsync(customer, cancellationToken);
                await _uow.SaveChangesAsync(cancellationToken);
            }
            else
            {
                if (!string.IsNullOrEmpty(dto.Email) && string.IsNullOrEmpty(customer.Email))
                {
                    customer.Email = dto.Email;
                }
            }

            var bookingCode = $"BK-{localBookingDateTime:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..4].ToUpper()}";
            var holdExpiresAt = nowUtc.AddMinutes(_options.Booking.HoldMinutes);

            var booking = new Booking
            {
                BookingCode = bookingCode,
                TableId = table.Id,
                CustomerId = customer.Id,
                CustomerName = dto.CustomerName,
                PhoneNumber = dto.PhoneNumber,
                GuestCount = dto.GuestCount,
                StartTime = bookingDateTimeUtc,
                EndTime = bookingDateTimeUtc.AddHours(2),
                DepositAmount = depositAmount,
                Status = BookingStatus.PendingPayment,
                HoldExpiresAt = holdExpiresAt,
                SpecialRequests = dto.SpecialRequest,
                CreatedAt = nowUtc
            };

            await _uow.Bookings.AddAsync(booking, cancellationToken);
            await _uow.SaveChangesAsync(cancellationToken);

            return Result<BookingConfirmationDto>.Success(new BookingConfirmationDto(
                booking.Id,
                booking.BookingCode,
                table.TableNumber,
                table.Area.AreaName,
                booking.CustomerName,
                booking.PhoneNumber,
                booking.StartTime,
                booking.GuestCount,
                booking.DepositAmount,
                booking.Status,
                booking.HoldExpiresAt
            ));
        }, cancellationToken);
    }

    public async Task<Result<BookingConfirmationDto>> ConfirmBookingPaymentAsync(int bookingId, string transactionRef, string provider, CancellationToken cancellationToken = default)
    {
        var booking = await _uow.Bookings.GetAll()
            .Include(b => b.Table)
                .ThenInclude(t => t.Area)
            .FirstOrDefaultAsync(b => b.Id == bookingId, cancellationToken);

        if (booking == null)
            return Result<BookingConfirmationDto>.Failure(Error.NotFound("Booking.NotFound", "Không tìm thấy thông tin đặt bàn."));

        if (booking.Status == BookingStatus.Confirmed)
        {
            return Result<BookingConfirmationDto>.Success(new BookingConfirmationDto(
                booking.Id,
                booking.BookingCode,
                booking.Table.TableNumber,
                booking.Table.Area.AreaName,
                booking.CustomerName,
                booking.PhoneNumber,
                booking.StartTime,
                booking.GuestCount,
                booking.DepositAmount,
                booking.Status,
                booking.HoldExpiresAt
            ));
        }

        if (booking.Status != BookingStatus.PendingPayment)
        {
            return Result<BookingConfirmationDto>.Failure(Error.Validation("Booking.InvalidStatus", $"Đơn đặt bàn ở trạng thái không thể thanh toán ({booking.Status})."));
        }

        if (DateTime.UtcNow > booking.HoldExpiresAt)
        {
            booking.Status = BookingStatus.Cancelled;
            await _uow.SaveChangesAsync(cancellationToken);
            return Result<BookingConfirmationDto>.Failure(Error.Validation("Booking.Expired", "Đơn đặt bàn đã quá hạn 15 phút thanh toán cọc và đã bị hủy."));
        }

        return await _uow.ExecuteInTransactionAsync(async () =>
        {
            booking.Status = BookingStatus.Confirmed;

            var transaction = new PaymentTransaction
            {
                BookingId = booking.Id,
                Amount = booking.DepositAmount,
                Type = PaymentTransactionType.DepositCharge,
                Method = PaymentMethod.BankTransfer,
                Status = TransactionStatus.Success,
                TxnRef = transactionRef,
                Provider = provider,
                CreatedAt = DateTime.UtcNow,
                CompletedAt = DateTime.UtcNow
            };

            await _uow.PaymentTransactions.AddAsync(transaction, cancellationToken);
            await _uow.SaveChangesAsync(cancellationToken);

            return Result<BookingConfirmationDto>.Success(new BookingConfirmationDto(
                booking.Id,
                booking.BookingCode,
                booking.Table.TableNumber,
                booking.Table.Area.AreaName,
                booking.CustomerName,
                booking.PhoneNumber,
                booking.StartTime,
                booking.GuestCount,
                booking.DepositAmount,
                booking.Status,
                booking.HoldExpiresAt
            ));
        }, cancellationToken);
    }

    public async Task<Result<BookingConfirmationDto>> GetBookingByCodeAsync(string bookingCode, string phoneNumber, CancellationToken cancellationToken = default)
    {
        var booking = await _uow.Bookings.GetAll()
            .Include(b => b.Table)
                .ThenInclude(t => t.Area)
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.BookingCode == bookingCode.Trim() && b.PhoneNumber == phoneNumber.Trim(), cancellationToken);

        if (booking == null)
            return Result<BookingConfirmationDto>.Failure(Error.NotFound("Booking.NotFound", "Không tìm thấy đơn đặt bàn với mã và số điện thoại đã cung cấp."));

        return Result<BookingConfirmationDto>.Success(new BookingConfirmationDto(
            booking.Id,
            booking.BookingCode,
            booking.Table.TableNumber,
            booking.Table.Area.AreaName,
            booking.CustomerName,
            booking.PhoneNumber,
            booking.StartTime,
            booking.GuestCount,
            booking.DepositAmount,
            booking.Status,
            booking.HoldExpiresAt
        ));
    }

    public async Task<Result<BookingConfirmationDto>> GetBookingByIdAsync(int bookingId, CancellationToken cancellationToken = default)
    {
        var booking = await _uow.Bookings.GetAll()
            .Include(b => b.Table)
                .ThenInclude(t => t.Area)
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == bookingId, cancellationToken);

        if (booking == null)
            return Result<BookingConfirmationDto>.Failure(Error.NotFound("Booking.NotFound", "Không tìm thấy thông tin đặt bàn."));

        return Result<BookingConfirmationDto>.Success(new BookingConfirmationDto(
            booking.Id,
            booking.BookingCode,
            booking.Table.TableNumber,
            booking.Table.Area.AreaName,
            booking.CustomerName,
            booking.PhoneNumber,
            booking.StartTime,
            booking.GuestCount,
            booking.DepositAmount,
            booking.Status,
            booking.HoldExpiresAt
        ));
    }

    public async Task<Result<CancelBookingResultDto>> CancelBookingAsync(CancelBookingDto dto, CancellationToken cancellationToken = default)
    {
        var booking = await _uow.Bookings.GetAll()
            .Include(b => b.Table)
            .FirstOrDefaultAsync(b => b.BookingCode == dto.BookingCode.Trim() && b.PhoneNumber == dto.PhoneNumber.Trim(), cancellationToken);

        if (booking == null)
            return Result<CancelBookingResultDto>.Failure(Error.NotFound("Booking.NotFound", "Không tìm thấy đơn đặt bàn khớp thông tin."));

        if (booking.Status == BookingStatus.Cancelled || booking.Status == BookingStatus.Expired)
            return Result<CancelBookingResultDto>.Failure(Error.Validation("Booking.AlreadyCancelled", "Đơn đặt bàn này đã bị hủy trước đó."));

        if (booking.Status == BookingStatus.CheckedIn || booking.Status == BookingStatus.Completed)
            return Result<CancelBookingResultDto>.Failure(Error.Validation("Booking.InvalidStatus", "Đơn đặt bàn đang được phục vụ hoặc đã hoàn tất, không thể hủy."));

        var nowUtc = DateTime.UtcNow;
        var (rate, refundAmount, reason) = _refundPolicy.CalculateRefund(booking.DepositAmount, booking.StartTime, nowUtc);

        return await _uow.ExecuteInTransactionAsync(async () =>
        {
            booking.Status = BookingStatus.Cancelled;
            booking.CancelledAt = nowUtc;

            if (refundAmount > 0)
            {
                var refundTxn = new PaymentTransaction
                {
                    BookingId = booking.Id,
                    Amount = refundAmount,
                    Type = PaymentTransactionType.DepositRefund,
                    Method = PaymentMethod.BankTransfer,
                    Status = TransactionStatus.Success,
                    TxnRef = $"REFUND-{Guid.NewGuid().ToString("N")[..8].ToUpper()}",
                    Provider = "AutoRefundGateway",
                    CreatedAt = nowUtc,
                    CompletedAt = nowUtc
                };
                await _uow.PaymentTransactions.AddAsync(refundTxn, cancellationToken);
            }

            await _uow.SaveChangesAsync(cancellationToken);

            return Result<CancelBookingResultDto>.Success(new CancelBookingResultDto(
                booking.BookingCode,
                booking.DepositAmount,
                rate,
                refundAmount,
                reason
            ));
        }, cancellationToken);
    }

    public async Task<List<BookingConfirmationDto>> GetTodayBookingsAsync(CancellationToken cancellationToken = default)
    {
        var today = DateTime.UtcNow.Date;
        var tomorrow = today.AddDays(1);

        var bookings = await _uow.Bookings.GetAll()
            .Include(b => b.Table)
                .ThenInclude(t => t.Area)
            .Where(b => b.StartTime >= today && b.StartTime < tomorrow)
            .OrderBy(b => b.StartTime)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return bookings.Select(b => new BookingConfirmationDto(
            b.Id,
            b.BookingCode,
            b.Table.TableNumber,
            b.Table.Area.AreaName,
            b.CustomerName,
            b.PhoneNumber,
            b.StartTime,
            b.GuestCount,
            b.DepositAmount,
            b.Status,
            b.HoldExpiresAt
        )).ToList();
    }
}
