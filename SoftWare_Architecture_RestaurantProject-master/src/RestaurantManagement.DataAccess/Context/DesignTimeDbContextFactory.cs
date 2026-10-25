using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace RestaurantManagement.DataAccess.Context;

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<RestaurantDbContext>
{
    public RestaurantDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<RestaurantDbContext>();
        optionsBuilder.UseNpgsql("Host=127.0.0.1;Port=4321;Database=restaurant_db;Username=postgres;Password=123", npgsqlOptions =>
        {
            npgsqlOptions.MigrationsAssembly(typeof(RestaurantDbContext).Assembly.FullName);
        })
        .UseSnakeCaseNamingConvention();

        return new RestaurantDbContext(optionsBuilder.Options);
    }
}
