using RestaurantManagement.Common.Enums;

namespace RestaurantManagement.DataAccess.Entities;

public class KotTicket
{
    public int Id { get; set; }
    public string TicketCode { get; set; } = string.Empty; // KOT-20261014-XXXX
    public int OrderId { get; set; }
    public Order Order { get; set; } = null!;
    public KitchenStation Station { get; set; } = KitchenStation.HotKitchen;
    public KotTicketType Type { get; set; } = KotTicketType.New;
    public string PayloadJson { get; set; } = string.Empty; // Danh sách món, số lượng, ghi chú
    public KotTicketStatus Status { get; set; } = KotTicketStatus.Queued;
    public int Attempts { get; set; } = 0;
    public string RequestId { get; set; } = string.Empty; // Chống in trùng lặp
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? PrintedAt { get; set; }
}

public class Printer
{
    public int Id { get; set; }
    public KitchenStation Station { get; set; } = KitchenStation.HotKitchen;
    public string Name { get; set; } = string.Empty;
    public string IpAddress { get; set; } = "127.0.0.1";
    public int Port { get; set; } = 9100;
    public bool IsBackup { get; set; } = false;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class AuditLog
{
    public int Id { get; set; }
    public int? StaffId { get; set; }
    public Staff? Staff { get; set; }
    public string Action { get; set; } = string.Empty; // Create, Update, Void, Refund, PinOverride
    public string EntityName { get; set; } = string.Empty; // Invoice, Booking, Order, MenuItem
    public string? EntityId { get; set; }
    public string? Details { get; set; }
    public string? IpAddress { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
