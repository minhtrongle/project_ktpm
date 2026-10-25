using RestaurantManagement.Common.Enums;

namespace RestaurantManagement.Business.DTOs.Auth;

public record LoginDto(string Username, string Password);

public record StaffAuthDto(
    int Id,
    string StaffCode,
    string FullName,
    string Username,
    StaffRole Role
);
