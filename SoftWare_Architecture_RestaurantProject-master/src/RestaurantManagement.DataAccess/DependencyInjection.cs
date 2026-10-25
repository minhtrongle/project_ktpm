using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RestaurantManagement.DataAccess.Context;
using RestaurantManagement.DataAccess.Repositories;
using RestaurantManagement.DataAccess.UnitOfWork;

namespace RestaurantManagement.DataAccess;

public static class DependencyInjection
{
    public static IServiceCollection AddDataAccessLayer(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? "Host=127.0.0.1;Port=4321;Database=restaurant_db;Username=postgres;Password=123";

        services.AddDbContext<RestaurantDbContext>(options =>
        {
            options.UseNpgsql(connectionString, npgsqlOptions =>
            {
                npgsqlOptions.MigrationsAssembly(typeof(RestaurantDbContext).Assembly.FullName);
                npgsqlOptions.EnableRetryOnFailure(3);
            })
            .UseSnakeCaseNamingConvention();
        });

        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
        services.AddScoped<ITableRepository, TableRepository>();
        services.AddScoped<IBookingRepository, BookingRepository>();
        services.AddScoped<IStaffRepository, StaffRepository>();
        services.AddScoped<IUnitOfWork, RestaurantManagement.DataAccess.UnitOfWork.UnitOfWork>();

        return services;
    }
}
