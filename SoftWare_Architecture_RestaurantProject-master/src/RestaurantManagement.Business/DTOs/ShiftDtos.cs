using RestaurantManagement.Common.Enums;

namespace RestaurantManagement.Business.DTOs;

public record CashDenomination(
    int Denomination,
    int Quantity
)
{
    public decimal Total => Denomination * Quantity;
}

public record ShiftSessionDto(
    int Id,
    string ShiftCode,
    string CounterCode,
    int StaffId,
    string StaffName,
    DateTime StartTime,
    DateTime? EndTime,
    decimal InitialCash,
    decimal TheoreticalCash,
    decimal? ActualCash,
    decimal? Difference,
    string? CashCountJson,
    string? Note,
    ShiftStatus Status,
    string? ApprovedByName,
    int TotalInvoicesCount,
    decimal TotalCashCollected,
    decimal TotalTransferCollected
);

public record OpenShiftDto(
    int StaffId,
    decimal InitialCash = 2000000,
    string CounterCode = "POS-01"
);

public record CloseShiftDto(
    int ShiftSessionId,
    int StaffId,
    List<CashDenomination> Denominations,
    string? Note,
    string? ManagerPin = null
);
