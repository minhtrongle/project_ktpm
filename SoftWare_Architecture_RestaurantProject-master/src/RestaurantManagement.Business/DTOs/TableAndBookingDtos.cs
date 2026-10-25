using RestaurantManagement.Common.Enums;

namespace RestaurantManagement.Business.DTOs;

public record TableDto(
    int Id,
    string TableNumber,
    int AreaId,
    string AreaName,
    int Capacity,
    TableStatus Status,
    string? CurrentOrderCode = null,
    int? CurrentGuests = null,
    string? BookingCustomer = null
);

public record AreaWithTablesDto(
    int AreaId,
    string AreaName,
    string? Description,
    List<TableDto> Tables
);

public record SearchAvailableTablesQueryDto(
    DateOnly BookingDate,
    TimeOnly BookingTime,
    int GuestCount,
    int? AreaId = null
);

public record AvailableTableDto(
    int TableId,
    string TableNumber,
    int AreaId,
    string AreaName,
    int Capacity
);

public record CreateBookingDto(
    int TableId,
    string CustomerName,
    string PhoneNumber,
    string? Email,
    DateOnly BookingDate,
    TimeOnly BookingTime,
    int GuestCount,
    string? SpecialRequest
);

public record BookingConfirmationDto(
    int BookingId,
    string BookingCode,
    string TableNumber,
    string AreaName,
    string CustomerName,
    string PhoneNumber,
    DateTime BookingDateTimeUtc,
    int GuestCount,
    decimal DepositAmount,
    BookingStatus Status,
    DateTime ExpiresAtUtc
);

public record CancelBookingDto(
    string BookingCode,
    string PhoneNumber,
    string? Reason = null
);

public record CancelBookingResultDto(
    string BookingCode,
    decimal DepositAmount,
    decimal RefundRate,
    decimal RefundAmount,
    string Explanation
);

public record SeatWalkInDto(
    int TableId,
    int StaffId,
    int GuestCount
);

public record CheckInBookingDto(
    int BookingId,
    int StaffId
);
