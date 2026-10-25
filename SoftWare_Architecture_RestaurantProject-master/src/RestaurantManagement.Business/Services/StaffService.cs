using Microsoft.AspNetCore.Identity;
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

public interface IStaffService
{
    Task<List<StaffDto>> GetAllStaffsAsync(CancellationToken cancellationToken = default);
    Task<Result<StaffDto>> GetStaffByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<Result<StaffDto>> CreateStaffAsync(CreateStaffDto dto, CancellationToken cancellationToken = default);
    Task<Result<StaffDto>> UpdateStaffAsync(UpdateStaffDto dto, CancellationToken cancellationToken = default);
    Task<Result<AttendanceDto>> CheckInAttendanceAsync(CheckInAttendanceDto dto, CancellationToken cancellationToken = default);
    Task<List<AttendanceDto>> GetAttendancesAsync(DateOnly? date = null, CancellationToken cancellationToken = default);
    Task<Result> ApproveAttendanceAsync(ApproveAttendanceDto dto, CancellationToken cancellationToken = default);
}

public class StaffService : IStaffService
{
    private readonly IUnitOfWork _uow;
    private readonly IPasswordHasher<Staff> _passwordHasher;
    private readonly LatePolicy _latePolicy;
    private readonly TimeZoneInfo _timeZone;

    public StaffService(
        IUnitOfWork uow,
        IPasswordHasher<Staff> passwordHasher,
        LatePolicy latePolicy,
        IOptions<RestaurantRulesOptions> options)
    {
        _uow = uow;
        _passwordHasher = passwordHasher;
        _latePolicy = latePolicy;

        try
        {
            _timeZone = TimeZoneInfo.FindSystemTimeZoneById(options.Value.TimeZone);
        }
        catch
        {
            _timeZone = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
        }
    }

    public async Task<List<StaffDto>> GetAllStaffsAsync(CancellationToken cancellationToken = default)
    {
        var staffs = await _uow.Staffs.GetAll()
            .OrderBy(s => s.Role)
            .ThenBy(s => s.StaffCode)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return staffs.Select(s => new StaffDto(
            s.Id,
            s.StaffCode,
            s.FullName,
            s.Username,
            s.Role,
            s.PhoneNumber,
            s.CitizenId,
            s.IsActive,
            s.CreatedAt
        )).ToList();
    }

    public async Task<Result<StaffDto>> GetStaffByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var s = await _uow.Staffs.GetByIdAsync(id, cancellationToken);
        if (s == null)
            return Result<StaffDto>.Failure(Error.NotFound("Staff.NotFound", "Không tìm thấy nhân viên."));

        return Result<StaffDto>.Success(new StaffDto(
            s.Id,
            s.StaffCode,
            s.FullName,
            s.Username,
            s.Role,
            s.PhoneNumber,
            s.CitizenId,
            s.IsActive,
            s.CreatedAt
        ));
    }

    public async Task<Result<StaffDto>> CreateStaffAsync(CreateStaffDto dto, CancellationToken cancellationToken = default)
    {
        var codeExists = await _uow.Staffs.GetAll().AnyAsync(s => s.StaffCode == dto.StaffCode.Trim(), cancellationToken);
        if (codeExists)
            return Result<StaffDto>.Failure(Error.Conflict("Staff.CodeExists", $"Mã nhân viên '{dto.StaffCode}' đã tồn tại."));

        var userExists = await _uow.Staffs.GetAll().AnyAsync(s => s.Username == dto.Username.Trim(), cancellationToken);
        if (userExists)
            return Result<StaffDto>.Failure(Error.Conflict("Staff.UsernameExists", $"Tên đăng nhập '{dto.Username}' đã tồn tại."));

        var citizenExists = await _uow.Staffs.GetAll().AnyAsync(s => s.CitizenId == dto.CitizenId.Trim(), cancellationToken);
        if (citizenExists)
            return Result<StaffDto>.Failure(Error.Conflict("Staff.CitizenExists", $"Số CCCD '{dto.CitizenId}' đã tồn tại trong hệ thống."));

        var staff = new Staff
        {
            StaffCode = dto.StaffCode.Trim(),
            FullName = dto.FullName.Trim(),
            Username = dto.Username.Trim(),
            Role = dto.Role,
            PhoneNumber = dto.PhoneNumber.Trim(),
            CitizenId = dto.CitizenId.Trim(),
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        staff.PasswordHash = _passwordHasher.HashPassword(staff, dto.Password);

        if (!string.IsNullOrWhiteSpace(dto.ApprovalPin))
        {
            staff.ApprovalPinHash = _passwordHasher.HashPassword(staff, dto.ApprovalPin.Trim());
        }

        await _uow.Staffs.AddAsync(staff, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);

        return Result<StaffDto>.Success(new StaffDto(
            staff.Id,
            staff.StaffCode,
            staff.FullName,
            staff.Username,
            staff.Role,
            staff.PhoneNumber,
            staff.CitizenId,
            staff.IsActive,
            staff.CreatedAt
        ));
    }

    public async Task<Result<StaffDto>> UpdateStaffAsync(UpdateStaffDto dto, CancellationToken cancellationToken = default)
    {
        var staff = await _uow.Staffs.GetByIdAsync(dto.Id, cancellationToken);
        if (staff == null)
            return Result<StaffDto>.Failure(Error.NotFound("Staff.NotFound", "Không tìm thấy nhân viên."));

        var citizenExists = await _uow.Staffs.GetAll().AnyAsync(s => s.CitizenId == dto.CitizenId.Trim() && s.Id != dto.Id, cancellationToken);
        if (citizenExists)
            return Result<StaffDto>.Failure(Error.Conflict("Staff.CitizenExists", $"Số CCCD '{dto.CitizenId}' đã tồn tại ở tài khoản khác."));

        staff.FullName = dto.FullName.Trim();
        staff.PhoneNumber = dto.PhoneNumber.Trim();
        staff.CitizenId = dto.CitizenId.Trim();
        staff.Role = dto.Role;
        staff.IsActive = dto.IsActive;

        if (!string.IsNullOrWhiteSpace(dto.NewPassword))
        {
            staff.PasswordHash = _passwordHasher.HashPassword(staff, dto.NewPassword);
        }

        if (!string.IsNullOrWhiteSpace(dto.ApprovalPin))
        {
            staff.ApprovalPinHash = _passwordHasher.HashPassword(staff, dto.ApprovalPin.Trim());
        }

        await _uow.SaveChangesAsync(cancellationToken);

        return Result<StaffDto>.Success(new StaffDto(
            staff.Id,
            staff.StaffCode,
            staff.FullName,
            staff.Username,
            staff.Role,
            staff.PhoneNumber,
            staff.CitizenId,
            staff.IsActive,
            staff.CreatedAt
        ));
    }

    public async Task<Result<AttendanceDto>> CheckInAttendanceAsync(CheckInAttendanceDto dto, CancellationToken cancellationToken = default)
    {
        var staff = await _uow.Staffs.GetByIdAsync(dto.StaffId, cancellationToken);
        if (staff == null)
            return Result<AttendanceDto>.Failure(Error.NotFound("Staff.NotFound", "Không tìm thấy nhân viên."));

        var nowUtc = DateTime.UtcNow;
        var localNow = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, _timeZone);
        var workDate = DateOnly.FromDateTime(localNow);

        // Kiểm tra xem hôm nay đã điểm danh chưa
        var existingAttendance = await _uow.Attendances.GetAll()
            .Include(a => a.ApprovedBy)
            .FirstOrDefaultAsync(a => a.StaffId == staff.Id && a.WorkDate == workDate, cancellationToken);

        if (existingAttendance != null)
        {
            return Result<AttendanceDto>.Success(new AttendanceDto(
                existingAttendance.Id,
                staff.Id,
                staff.StaffCode,
                staff.FullName,
                staff.Role,
                existingAttendance.WorkDate,
                existingAttendance.CheckInTime,
                existingAttendance.CheckOutTime,
                existingAttendance.IsLate,
                existingAttendance.LateMinutes,
                existingAttendance.Note,
                existingAttendance.ApprovedBy?.FullName
            ));
        }

        // Ca ngày: 08:00, Ca tối: 16:00
        var localTimeOfDay = TimeOnly.FromTimeSpan(localNow.TimeOfDay);
        var scheduledStartTime = localTimeOfDay < new TimeOnly(13, 0)
            ? new TimeOnly(8, 0)
            : new TimeOnly(16, 0);

        var (isLate, lateMinutes) = _latePolicy.CheckLate(scheduledStartTime, localTimeOfDay);

        var attendance = new Attendance
        {
            StaffId = staff.Id,
            WorkDate = workDate,
            CheckInTime = nowUtc,
            IsLate = isLate,
            LateMinutes = lateMinutes,
            Note = isLate ? $"Đi trễ {lateMinutes} phút so với giờ bắt đầu ca {scheduledStartTime:HH:mm}" : "Đúng giờ"
        };

        await _uow.Attendances.AddAsync(attendance, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);

        return Result<AttendanceDto>.Success(new AttendanceDto(
            attendance.Id,
            staff.Id,
            staff.StaffCode,
            staff.FullName,
            staff.Role,
            attendance.WorkDate,
            attendance.CheckInTime,
            attendance.CheckOutTime,
            attendance.IsLate,
            attendance.LateMinutes,
            attendance.Note,
            null
        ));
    }

    public async Task<List<AttendanceDto>> GetAttendancesAsync(DateOnly? date = null, CancellationToken cancellationToken = default)
    {
        var query = _uow.Attendances.GetAll()
            .Include(a => a.Staff)
            .Include(a => a.ApprovedBy)
            .AsNoTracking();

        if (date.HasValue)
        {
            query = query.Where(a => a.WorkDate == date.Value);
        }

        var attendances = await query
            .OrderByDescending(a => a.WorkDate)
            .ThenByDescending(a => a.CheckInTime)
            .Take(50)
            .ToListAsync(cancellationToken);

        return attendances.Select(a => new AttendanceDto(
            a.Id,
            a.StaffId,
            a.Staff.StaffCode,
            a.Staff.FullName,
            a.Staff.Role,
            a.WorkDate,
            a.CheckInTime,
            a.CheckOutTime,
            a.IsLate,
            a.LateMinutes,
            a.Note,
            a.ApprovedBy?.FullName
        )).ToList();
    }

    public async Task<Result> ApproveAttendanceAsync(ApproveAttendanceDto dto, CancellationToken cancellationToken = default)
    {
        var attendance = await _uow.Attendances.GetByIdAsync(dto.AttendanceId, cancellationToken);
        if (attendance == null)
            return Result.Failure(Error.NotFound("Attendance.NotFound", "Không tìm thấy bản ghi chấm công."));

        var manager = await _uow.Staffs.GetByIdAsync(dto.ManagerStaffId, cancellationToken);
        if (manager == null || manager.Role != StaffRole.Manager)
            return Result.Failure(Error.Validation("Auth.Forbidden", "Chỉ Quản lý mới có quyền duyệt gỡ cờ đi trễ."));

        attendance.IsLate = false;
        attendance.ApprovedById = manager.Id;
        attendance.Note = $"{attendance.Note} [QUẢN LÝ {manager.FullName} ĐÃ DUYỆT GỠ TRỄ: {dto.Note}]";

        await _uow.AuditLogs.AddAsync(new AuditLog
        {
            Action = "ApproveAttendance",
            EntityName = "Attendances",
            EntityId = attendance.Id.ToString(),
            Details = $"Gỡ trễ bởi {manager.FullName}. Lý do: {dto.Note}",
            CreatedAt = DateTime.UtcNow
        }, cancellationToken);

        await _uow.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
