using RestaurantManagement.Common.Enums;

namespace RestaurantManagement.DataAccess.Entities;

public class Area
{
    public int Id { get; set; }
    public string AreaName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int DisplayOrder { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<DiningTable> Tables { get; set; } = new List<DiningTable>();
}

public class DiningTable
{
    public int Id { get; set; }
    public string TableNumber { get; set; } = string.Empty; // T01..T15, S01..S10, L01..L10, V01..V05
    public int AreaId { get; set; }
    public Area Area { get; set; } = null!;
    public int Capacity { get; set; } // 2 - 20
    public TableStatus Status { get; set; } = TableStatus.Available;
    public uint Xmin { get; set; } // PostgreSQL row concurrency token
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
    public ICollection<Order> Orders { get; set; } = new List<Order>();
}

public class Customer
{
    public int Id { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string? Email { get; set; }
    public int TotalPoints { get; set; } = 0;
    public MembershipRank Rank { get; set; } = MembershipRank.Standard;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
    public ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
}

public class Booking
{
    public int Id { get; set; }
    public string BookingCode { get; set; } = string.Empty; // BK-20261014-XXXX
    public int TableId { get; set; }
    public DiningTable Table { get; set; } = null!;
    public int? CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public int GuestCount { get; set; }
    public DateTime StartTime { get; set; } // UTC
    public DateTime EndTime { get; set; }   // UTC
    public DateTime HoldExpiresAt { get; set; } // UTC (15 mins from hold creation)
    public decimal DepositAmount { get; set; }
    public BookingStatus Status { get; set; } = BookingStatus.PendingPayment;
    public BookingSource Source { get; set; } = BookingSource.Online;
    public string? SpecialRequests { get; set; }
    public DateTime? CancelledAt { get; set; }
    public DateTime? CheckedInAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<PaymentTransaction> PaymentTransactions { get; set; } = new List<PaymentTransaction>();
    public ICollection<Order> Orders { get; set; } = new List<Order>();
    public ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
}
