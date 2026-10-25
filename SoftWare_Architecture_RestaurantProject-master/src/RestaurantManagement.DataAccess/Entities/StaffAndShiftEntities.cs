using RestaurantManagement.Common.Enums;

namespace RestaurantManagement.DataAccess.Entities;

public class Staff
{
    public int Id { get; set; }
    public string StaffCode { get; set; } = string.Empty; // NV0001
    public string FullName { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public StaffRole Role { get; set; } = StaffRole.Staff;
    public string PhoneNumber { get; set; } = string.Empty;
    public string CitizenId { get; set; } = string.Empty; // CCCD
    public string? ApprovalPinHash { get; set; } // PIN 4-6 số cho Quản lý duyệt
    public bool IsActive { get; set; } = true;
    public int FailedLoginCount { get; set; } = 0;
    public DateTime? LockoutEnd { get; set; } // Khóa 15' khi quá 5 lần
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<ShiftSession> ShiftSessions { get; set; } = new List<ShiftSession>();
    public ICollection<Attendance> Attendances { get; set; } = new List<Attendance>();
    public ICollection<Order> CreatedOrders { get; set; } = new List<Order>();
    public ICollection<Invoice> CreatedInvoices { get; set; } = new List<Invoice>();
    public ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();
}

public class ShiftSession
{
    public int Id { get; set; }
    public string ShiftCode { get; set; } = string.Empty; // SH-20261014-01
    public string CounterCode { get; set; } = "POS-01";
    public int StaffId { get; set; }
    public Staff Staff { get; set; } = null!;
    public DateTime StartTime { get; set; } = DateTime.UtcNow;
    public DateTime? EndTime { get; set; }
    public decimal InitialCash { get; set; } = 2000000;
    public decimal TheoreticalCash { get; set; } = 0;
    public decimal? ActualCash { get; set; }
    public string? CashCountJson { get; set; } // JSON bảng kê mệnh giá tiền
    public decimal? Difference { get; set; }
    public string? Note { get; set; }
    public ShiftStatus Status { get; set; } = ShiftStatus.Active;
    public int? ApprovedById { get; set; }
    public Staff? ApprovedBy { get; set; }
}

public class Attendance
{
    public int Id { get; set; }
    public int StaffId { get; set; }
    public Staff Staff { get; set; } = null!;
    public DateOnly WorkDate { get; set; }
    public DateTime CheckInTime { get; set; } = DateTime.UtcNow;
    public DateTime? CheckOutTime { get; set; }
    public bool IsLate { get; set; } = false;
    public int LateMinutes { get; set; } = 0;
    public string? Note { get; set; }
    public int? ApprovedById { get; set; } // Quản lý gỡ cờ trễ
    public Staff? ApprovedBy { get; set; }
}
