using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RestaurantManagement.Business.Options;
using RestaurantManagement.Business.Policies;
using RestaurantManagement.Business.Services;
using RestaurantManagement.DataAccess;
using RestaurantManagement.DataAccess.Entities;

namespace RestaurantManagement.Business;

public static class DependencyInjection
{
    public static IServiceCollection AddBusinessLayer(this IServiceCollection services, IConfiguration configuration)
    {
        // 1. Đăng ký DataAccess Layer (tầng dưới)
        services.AddDataAccessLayer(configuration);

        // 2. Cấu hình Options
        services.Configure<RestaurantRulesOptions>(configuration.GetSection(RestaurantRulesOptions.SectionName));

        // 3. Thời gian hệ thống TimeProvider
        services.AddSingleton(TimeProvider.System);

        // 4. Password Hasher (PBKDF2)
        services.AddScoped<IPasswordHasher<Staff>, PasswordHasher<Staff>>();

        // 5. Đăng ký Policies
        services.AddSingleton(sp =>
        {
            var options = sp.GetRequiredService<IOptions<RestaurantRulesOptions>>().Value;
            return new DepositPolicy(options.Deposit);
        });

        services.AddSingleton(sp =>
        {
            var options = sp.GetRequiredService<IOptions<RestaurantRulesOptions>>().Value;
            return new PeakHourPolicy(options.Billing, options.TimeZone);
        });

        services.AddSingleton(sp =>
        {
            var options = sp.GetRequiredService<IOptions<RestaurantRulesOptions>>().Value;
            var peakHourPolicy = sp.GetRequiredService<PeakHourPolicy>();
            return new BillingCalculator(options.Billing, peakHourPolicy);
        });

        services.AddSingleton<RefundPolicy>();

        services.AddSingleton(sp =>
        {
            var options = sp.GetRequiredService<IOptions<RestaurantRulesOptions>>().Value;
            return new LatePolicy(options.Shift);
        });

        // 6. Đăng ký Services
        services.AddScoped<IDatabaseInitializer, DatabaseInitializer>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<ITableService, TableService>();
        services.AddScoped<IBookingService, BookingService>();
        services.AddScoped<IOrderService, OrderService>();
        services.AddScoped<IBillingService, BillingService>();
        services.AddScoped<IShiftService, ShiftService>();
        services.AddScoped<IMenuService, MenuService>();
        services.AddScoped<IStaffService, StaffService>();
        services.AddScoped<IReportService, ReportService>();

        return services;
    }
}
