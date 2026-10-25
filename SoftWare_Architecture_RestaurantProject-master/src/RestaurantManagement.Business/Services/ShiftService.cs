using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RestaurantManagement.Business.DTOs;
using RestaurantManagement.Common.Enums;
using RestaurantManagement.Common.Results;
using RestaurantManagement.DataAccess.Entities;
using RestaurantManagement.DataAccess.UnitOfWork;

namespace RestaurantManagement.Business.Services;

public interface IShiftService
{
    Task<Result<ShiftSessionDto>> GetCurrentActiveShiftAsync(CancellationToken cancellationToken = default);
    Task<Result<ShiftSessionDto>> OpenShiftAsync(OpenShiftDto dto, CancellationToken cancellationToken = default);
    Task<Result<ShiftSessionDto>> CloseShiftAsync(CloseShiftDto dto, CancellationToken cancellationToken = default);
    Task<List<ShiftSessionDto>> GetShiftHistoryAsync(DateOnly? date = null, CancellationToken cancellationToken = default);
}

public class ShiftService : IShiftService
{
    private readonly IUnitOfWork _uow;
    private readonly IPasswordHasher<Staff> _passwordHasher;

    public ShiftService(IUnitOfWork uow, IPasswordHasher<Staff> passwordHasher)
    {
        _uow = uow;
        _passwordHasher = passwordHasher;
    }

    public async Task<Result<ShiftSessionDto>> GetCurrentActiveShiftAsync(CancellationToken cancellationToken = default)
    {
        var shift = await _uow.ShiftSessions.GetAll()
            .Include(s => s.Staff)
            .Include(s => s.ApprovedBy)
            .Where(s => s.Status == ShiftStatus.Active)
            .OrderByDescending(s => s.StartTime)
            .FirstOrDefaultAsync(cancellationToken);

        if (shift == null)
            return Result<ShiftSessionDto>.Failure(Error.NotFound("Shift.NoActiveShift", "Hiện tại chưa có ca trực nào được mở."));

        return Result<ShiftSessionDto>.Success(await EnrichShiftDtoAsync(shift, cancellationToken));
    }

    public async Task<Result<ShiftSessionDto>> OpenShiftAsync(OpenShiftDto dto, CancellationToken cancellationToken = default)
    {
        var hasActiveShift = await _uow.ShiftSessions.GetAll()
            .AnyAsync(s => s.Status == ShiftStatus.Active, cancellationToken);

        if (hasActiveShift)
        {
            return Result<ShiftSessionDto>.Failure(Error.Conflict(
                "Shift.AlreadyActive",
                "Đang có một ca trực khác đang hoạt động. Vui lòng đóng ca hiện tại trước khi mở ca mới."));
        }

        var staff = await _uow.Staffs.GetByIdAsync(dto.StaffId, cancellationToken);
        if (staff == null)
            return Result<ShiftSessionDto>.Failure(Error.NotFound("Staff.NotFound", "Không tìm thấy nhân viên mở ca."));

        if (dto.InitialCash < 0)
            return Result<ShiftSessionDto>.Failure(Error.Validation("Shift.InvalidInitialCash", "Số tiền nhận quỹ đầu ca không thể là số âm."));

        var nowUtc = DateTime.UtcNow;
        var shiftCode = $"SH-{nowUtc:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..4].ToUpper()}";

        var shift = new ShiftSession
        {
            ShiftCode = shiftCode,
            CounterCode = dto.CounterCode,
            StaffId = dto.StaffId,
            StartTime = nowUtc,
            InitialCash = dto.InitialCash,
            TheoreticalCash = dto.InitialCash,
            Status = ShiftStatus.Active
        };

        await _uow.ShiftSessions.AddAsync(shift, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);

        return Result<ShiftSessionDto>.Success(await EnrichShiftDtoAsync(shift, cancellationToken));
    }

    public async Task<Result<ShiftSessionDto>> CloseShiftAsync(CloseShiftDto dto, CancellationToken cancellationToken = default)
    {
        var shift = await _uow.ShiftSessions.GetAll()
            .Include(s => s.Staff)
            .FirstOrDefaultAsync(s => s.Id == dto.ShiftSessionId, cancellationToken);

        if (shift == null)
            return Result<ShiftSessionDto>.Failure(Error.NotFound("Shift.NotFound", "Không tìm thấy ca trực."));

        if (shift.Status != ShiftStatus.Active)
            return Result<ShiftSessionDto>.Failure(Error.Validation("Shift.NotActive", "Ca trực này đã đóng trước đó."));

        // Tính tiền mặt và chuyển khoản phát sinh trong ca
        var invoices = await _uow.Invoices.GetAll()
            .Where(i => i.Status == InvoiceStatus.Paid && i.CreatedAt >= shift.StartTime)
            .ToListAsync(cancellationToken);

        var cashCollected = invoices.Where(i => i.PaymentMethod == PaymentMethod.Cash).Sum(i => i.FinalAmount);
        var transferCollected = invoices.Where(i => i.PaymentMethod == PaymentMethod.BankTransfer).Sum(i => i.FinalAmount);

        var theoreticalCash = shift.InitialCash + cashCollected;
        var actualCash = dto.Denominations.Sum(d => d.Total);
        var difference = actualCash - theoreticalCash;

        // Nếu có chênh lệch tiền mặt, bắt buộc phải có giải trình và mã PIN Quản lý duyệt
        int? approvedById = null;
        if (difference != 0)
        {
            if (string.IsNullOrWhiteSpace(dto.Note))
            {
                return Result<ShiftSessionDto>.Failure(Error.Validation(
                    "Shift.ExplanationRequired",
                    $"Phát hiện chênh lệch két tiền mặt ({difference:N0}đ so với lý thuyết {theoreticalCash:N0}đ). Bắt buộc phải nhập giải trình lý do chênh lệch."));
            }

            var manager = await ValidateAndGetManagerAsync(dto.ManagerPin, cancellationToken);
            if (manager == null)
            {
                return Result<ShiftSessionDto>.Failure(Error.Validation(
                    "Auth.InvalidPin",
                    "Chênh lệch két quỹ ca trực bắt buộc phải được Quản lý kiểm tra và nhập mã PIN xác thực để đóng ca."));
            }
            approvedById = manager.Id;
        }

        var nowUtc = DateTime.UtcNow;

        shift.EndTime = nowUtc;
        shift.TheoreticalCash = theoreticalCash;
        shift.ActualCash = actualCash;
        shift.Difference = difference;
        shift.CashCountJson = JsonSerializer.Serialize(dto.Denominations);
        shift.Note = dto.Note;
        shift.Status = ShiftStatus.Closed;
        shift.ApprovedById = approvedById;

        await _uow.AuditLogs.AddAsync(new AuditLog
        {
            Action = "CloseShift",
            EntityName = "ShiftSessions",
            EntityId = shift.Id.ToString(),
            Details = $"Đóng lúc {nowUtc:HH:mm dd/MM/yyyy}. Thực tế {actualCash:N0}đ, Lý thuyết {theoreticalCash:N0}đ, Lệch {difference:N0}đ. Note: {dto.Note}",
            CreatedAt = nowUtc
        }, cancellationToken);

        await _uow.SaveChangesAsync(cancellationToken);

        return Result<ShiftSessionDto>.Success(await EnrichShiftDtoAsync(shift, cancellationToken));
    }

    public async Task<List<ShiftSessionDto>> GetShiftHistoryAsync(DateOnly? date = null, CancellationToken cancellationToken = default)
    {
        var query = _uow.ShiftSessions.GetAll()
            .Include(s => s.Staff)
            .Include(s => s.ApprovedBy)
            .AsNoTracking();

        if (date.HasValue)
        {
            var from = date.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            var to = date.Value.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);
            query = query.Where(s => s.StartTime >= from && s.StartTime <= to);
        }

        var list = await query.OrderByDescending(s => s.StartTime).Take(30).ToListAsync(cancellationToken);

        var result = new List<ShiftSessionDto>();
        foreach (var s in list)
        {
            result.Add(await EnrichShiftDtoAsync(s, cancellationToken));
        }
        return result;
    }

    private async Task<ShiftSessionDto> EnrichShiftDtoAsync(ShiftSession shift, CancellationToken cancellationToken)
    {
        var endTime = shift.EndTime ?? DateTime.UtcNow;
        var invoices = await _uow.Invoices.GetAll()
            .Where(i => i.Status == InvoiceStatus.Paid && i.CreatedAt >= shift.StartTime && i.CreatedAt <= endTime)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var cash = invoices.Where(i => i.PaymentMethod == PaymentMethod.Cash).Sum(i => i.FinalAmount);
        var transfer = invoices.Where(i => i.PaymentMethod == PaymentMethod.BankTransfer).Sum(i => i.FinalAmount);
        var theoretical = shift.InitialCash + cash;

        return new ShiftSessionDto(
            shift.Id,
            shift.ShiftCode,
            shift.CounterCode,
            shift.StaffId,
            shift.Staff?.FullName ?? "Thu ngân",
            shift.StartTime,
            shift.EndTime,
            shift.InitialCash,
            shift.Status == ShiftStatus.Active ? theoretical : shift.TheoreticalCash,
            shift.ActualCash,
            shift.Difference,
            shift.CashCountJson,
            shift.Note,
            shift.Status,
            shift.ApprovedBy?.FullName,
            invoices.Count,
            cash,
            transfer
        );
    }

    private async Task<Staff?> ValidateAndGetManagerAsync(string? pin, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(pin)) return null;

        var managers = await _uow.Staffs.GetAll()
            .Where(s => s.Role == StaffRole.Manager && s.IsActive && s.ApprovalPinHash != null)
            .ToListAsync(cancellationToken);

        foreach (var mgr in managers)
        {
            var verify = _passwordHasher.VerifyHashedPassword(mgr, mgr.ApprovalPinHash!, pin);
            if (verify == PasswordVerificationResult.Success)
                return mgr;
        }

        return null;
    }
}
