using Microsoft.AspNetCore.Identity;
using RestaurantManagement.Business.DTOs.Auth;
using RestaurantManagement.Common.Enums;
using RestaurantManagement.Common.Results;
using RestaurantManagement.DataAccess.Entities;
using RestaurantManagement.DataAccess.UnitOfWork;

namespace RestaurantManagement.Business.Services;

public interface IAuthService
{
    Task<Result<StaffAuthDto>> LoginAsync(LoginDto dto, CancellationToken cancellationToken = default);
    Task<Result<bool>> VerifyManagerPinAsync(int staffId, string pin, CancellationToken cancellationToken = default);
}

public class AuthService : IAuthService
{
    private readonly IUnitOfWork _uow;
    private readonly IPasswordHasher<Staff> _passwordHasher;
    private readonly TimeProvider _clock;

    public AuthService(
        IUnitOfWork uow,
        IPasswordHasher<Staff> passwordHasher,
        TimeProvider clock)
    {
        _uow = uow;
        _passwordHasher = passwordHasher;
        _clock = clock;
    }

    public async Task<Result<StaffAuthDto>> LoginAsync(LoginDto dto, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Username) || string.IsNullOrWhiteSpace(dto.Password))
            return Result<StaffAuthDto>.Failure("Auth.InvalidInput", "Tên đăng nhập và mật khẩu không được để trống.");

        var staff = await _uow.Staffs.GetByUsernameAsync(dto.Username.Trim().ToLowerInvariant(), cancellationToken);
        if (staff is null)
            return Result<StaffAuthDto>.Failure("Auth.InvalidCredentials", "Tên đăng nhập hoặc mật khẩu không chính xác.");

        if (!staff.IsActive)
            return Result<StaffAuthDto>.Failure("Auth.InactiveAccount", "Tài khoản nhân viên này đã bị ngưng hoạt động.");

        var now = _clock.GetUtcNow().UtcDateTime;
        if (staff.LockoutEnd.HasValue && staff.LockoutEnd.Value > now)
        {
            var remaining = Math.Ceiling((staff.LockoutEnd.Value - now).TotalMinutes);
            return Result<StaffAuthDto>.Failure("Auth.LockedOut",
                $"Tài khoản đang bị khóa do nhập sai quá 5 lần. Vui lòng thử lại sau {remaining} phút.");
        }

        var verifyResult = _passwordHasher.VerifyHashedPassword(staff, staff.PasswordHash, dto.Password);
        if (verifyResult == PasswordVerificationResult.Failed)
        {
            staff.FailedLoginCount++;
            if (staff.FailedLoginCount >= 5)
            {
                staff.LockoutEnd = now.AddMinutes(15);
            }
            await _uow.SaveChangesAsync(cancellationToken);

            var remainingAttempts = Math.Max(0, 5 - staff.FailedLoginCount);
            var msg = remainingAttempts > 0
                ? $"Tên đăng nhập hoặc mật khẩu không chính xác. Bạn còn {remainingAttempts} lần thử trước khi bị khóa 15 phút."
                : "Tài khoản của bạn đã bị khóa 15 phút do nhập sai 5 lần liên tiếp.";

            return Result<StaffAuthDto>.Failure("Auth.InvalidCredentials", msg);
        }

        // Đăng nhập thành công -> Reset đếm sai và cờ khóa
        staff.FailedLoginCount = 0;
        staff.LockoutEnd = null;
        await _uow.SaveChangesAsync(cancellationToken);

        var authDto = new StaffAuthDto(
            staff.Id,
            staff.StaffCode,
            staff.FullName,
            staff.Username,
            staff.Role
        );

        return Result<StaffAuthDto>.Success(authDto);
    }

    public async Task<Result<bool>> VerifyManagerPinAsync(int staffId, string pin, CancellationToken cancellationToken = default)
    {
        var staff = await _uow.Staffs.GetByIdAsync(staffId, cancellationToken);
        if (staff is null || staff.Role != StaffRole.Manager)
            return Result<bool>.Failure("Auth.Unauthorized", "Chỉ có tài khoản Quản lý mới có quyền phê duyệt.");

        if (string.IsNullOrWhiteSpace(staff.ApprovalPinHash))
            return Result<bool>.Failure("Auth.NoPinConfigured", "Quản lý chưa thiết lập mã PIN phê duyệt.");

        var verifyResult = _passwordHasher.VerifyHashedPassword(staff, staff.ApprovalPinHash, pin);
        if (verifyResult == PasswordVerificationResult.Failed)
            return Result<bool>.Failure("Auth.InvalidPin", "Mã PIN phê duyệt của Quản lý không chính xác.");

        return Result<bool>.Success(true);
    }
}
