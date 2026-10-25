using RestaurantManagement.Common.Enums;

namespace RestaurantManagement.DataAccess.Entities;

public class Order
{
    public int Id { get; set; }
    public string OrderCode { get; set; } = string.Empty; // ORD-20261014-XXXX
    public int TableId { get; set; }
    public DiningTable Table { get; set; } = null!;
    public int? BookingId { get; set; }
    public Booking? Booking { get; set; }
    public int GuestCount { get; set; }
    public decimal TotalAmount { get; set; } = 0;
    public TableStatus Status { get; set; } = TableStatus.Occupied;
    public int CreatedById { get; set; }
    public Staff CreatedBy { get; set; } = null!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ClosedAt { get; set; }
    public uint Xmin { get; set; } // concurrency token

    public ICollection<OrderDetail> OrderDetails { get; set; } = new List<OrderDetail>();
    public ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
    public ICollection<KotTicket> KotTickets { get; set; } = new List<KotTicket>();
}

public class OrderDetail
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public Order Order { get; set; } = null!;
    public int MenuItemId { get; set; }
    public MenuItem MenuItem { get; set; } = null!;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal SubTotal { get; set; }
    public string? Note { get; set; }
    public OrderItemStatus Status { get; set; } = OrderItemStatus.Pending;
    public DateTime? SentToKitchenAt { get; set; }
    public string? CancelReason { get; set; }
    public string? ReturnReason { get; set; }
    public string? ReturnType { get; set; } // Damaged, CustomerChanged...
    public int? ApprovedById { get; set; }
    public Staff? ApprovedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class Invoice
{
    public int Id { get; set; }
    public string InvoiceCode { get; set; } = string.Empty; // INV-20261014-XXXX
    public int OrderId { get; set; }
    public Order Order { get; set; } = null!;
    public int? BookingId { get; set; }
    public Booking? Booking { get; set; }
    public int? CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public int StaffId { get; set; }
    public Staff Staff { get; set; } = null!;
    public decimal TotalAmount { get; set; }      // Tổng tiền món ăn
    public decimal SurchargeAmount { get; set; }  // Phụ thu cao điểm cuối tuần
    public decimal VatAmount { get; set; }        // Thuế VAT (8%)
    public decimal DiscountAmount { get; set; }   // Giảm giá thành viên
    public decimal ManualDiscountAmount { get; set; } // Giảm giá tay (cần duyệt nếu > 20%)
    public decimal DepositDeducted { get; set; }  // Khấu trừ cọc
    public decimal FinalAmount { get; set; }      // Tiền thực thu
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Cash;
    public decimal CashReceived { get; set; }     // Khách đưa
    public decimal ChangeAmount { get; set; }     // Tiền thừa trả khách
    public InvoiceStatus Status { get; set; } = InvoiceStatus.Paid;
    public string? VoidReason { get; set; }
    public DateTime? VoidedAt { get; set; }
    public int? ApprovedById { get; set; }
    public Staff? ApprovedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class PaymentTransaction
{
    public int Id { get; set; }
    public int? BookingId { get; set; }
    public Booking? Booking { get; set; }
    public PaymentTransactionType Type { get; set; } = PaymentTransactionType.DepositCharge;
    public PaymentMethod Method { get; set; } = PaymentMethod.Gateway;
    public string Provider { get; set; } = "FakeGateway"; // FakeGateway / VNPay
    public string TxnRef { get; set; } = string.Empty;     // Unique mã tham chiếu giao dịch
    public decimal Amount { get; set; }
    public TransactionStatus Status { get; set; } = TransactionStatus.Pending;
    public string? RawPayload { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
}
