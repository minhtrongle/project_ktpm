using Microsoft.EntityFrameworkCore;
using RestaurantManagement.DataAccess.Context;
using RestaurantManagement.DataAccess.Seed;

namespace RestaurantManagement.Business.Services;

public interface IDatabaseInitializer
{
    Task InitializeAsync();
}

public class DatabaseInitializer : IDatabaseInitializer
{
    private readonly RestaurantDbContext _context;

    public DatabaseInitializer(RestaurantDbContext context)
    {
        _context = context;
    }

    public async Task InitializeAsync()
    {
        // 1. Chạy migration nếu còn thiếu
        await _context.Database.MigrateAsync();

        // 2. Chạy Seeder nếu dữ liệu ban đầu chưa có
        await DataSeeder.SeedAsync(_context);
    }
}
