using RestaurantManagement.Common.Enums;

namespace RestaurantManagement.Business.DTOs;

public record StaffDto(
    int Id,
    string StaffCode,
    string FullName,
    string Username,
    StaffRole Role,
    string PhoneNumber,
    string CitizenId,
    bool IsActive,
    DateTime CreatedAt
);

public record CreateStaffDto(
    string StaffCode,
    string FullName,
    string Username,
    string Password,
    StaffRole Role,
    string PhoneNumber,
    string CitizenId,
    string? ApprovalPin = null
);

public record UpdateStaffDto(
    int Id,
    string FullName,
    string PhoneNumber,
    string CitizenId,
    StaffRole Role,
    bool IsActive,
    string? NewPassword = null,
    string? ApprovalPin = null
);

public record AttendanceDto(
    int Id,
    int StaffId,
    string StaffCode,
    string StaffName,
    StaffRole Role,
    DateOnly WorkDate,
    DateTime CheckInTime,
    DateTime? CheckOutTime,
    bool IsLate,
    int LateMinutes,
    string? Note,
    string? ApprovedByName
);

public record CheckInAttendanceDto(
    int StaffId
);

public record ApproveAttendanceDto(
    int AttendanceId,
    int ManagerStaffId,
    string Note
);
