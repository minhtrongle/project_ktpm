using RestaurantManagement.Common.Enums;

namespace RestaurantManagement.Business.DTOs;

public record OrderItemDto(
    int Id,
    int MenuItemId,
    string ItemCode,
    string ItemName,
    string? Unit,
    int Quantity,
    decimal UnitPrice,
    decimal SubTotal,
    OrderItemStatus Status,
    KitchenStation KitchenStation,
    string? Note,
    DateTime CreatedAtUtc
);

public record OrderDto(
    int Id,
    string OrderCode,
    int TableId,
    string TableNumber,
    string AreaName,
    int GuestCount,
    TableStatus TableStatus,
    bool IsClosed,
    DateTime CreatedAtUtc,
    string StaffName,
    int? BookingId,
    decimal DepositAmount,
    List<OrderItemDto> Items,
    decimal TotalItemsAmount
);

public record AddOrderItemDto(
    int OrderId,
    int MenuItemId,
    int Quantity,
    string? Note
);

public record SendKotResultDto(
    int OrderId,
    List<string> TicketCodes,
    int ItemCount,
    DateTime SentAtUtc
);

public record ChangeOrderItemDto(
    int OrderDetailId,
    int NewMenuItemId,
    int NewQuantity,
    string Reason,
    string? ManagerPin = null
);

public record ReturnOrderItemDto(
    int OrderDetailId,
    string Reason,
    bool RestaurantFault,
    string? ManagerPin = null
);

public record PreBillDto(
    string OrderCode,
    string TableNumber,
    string AreaName,
    int GuestCount,
    DateTime OrderTime,
    List<OrderItemDto> Items,
    decimal ItemsTotal,
    decimal SurchargeAmount,
    decimal VatAmount,
    decimal DiscountAmount,
    decimal DepositDeducted,
    decimal FinalAmount
);

public record PrepareInvoiceDto(
    int OrderId,
    string? CustomerPhoneNumber = null,
    decimal ManualDiscountPercent = 0
);

public record InvoiceDto(
    int Id,
    string InvoiceCode,
    int OrderId,
    string OrderCode,
    string TableNumber,
    string AreaName,
    string? CustomerName,
    string? CustomerPhone,
    MembershipRank MembershipRank,
    decimal ItemsTotal,
    decimal SurchargeAmount,
    decimal VatAmount,
    decimal MemberDiscountAmount,
    decimal ManualDiscountAmount,
    decimal DepositDeducted,
    decimal FinalAmount,
    PaymentMethod PaymentMethod,
    InvoiceStatus Status,
    DateTime CreatedAtUtc,
    DateTime? PaidAtUtc,
    decimal? CashReceived,
    decimal? ChangeAmount,
    string? VietQrContent,
    string StaffName
);

public record ProcessPaymentDto(
    int InvoiceId,
    int CashierStaffId,
    PaymentMethod PaymentMethod,
    decimal CashReceived = 0
);
