using Microsoft.EntityFrameworkCore;
using RestaurantManagement.DataAccess.Entities;

namespace RestaurantManagement.DataAccess.Context;

public class RestaurantDbContext : DbContext
{
    public RestaurantDbContext(DbContextOptions<RestaurantDbContext> options) : base(options)
    {
    }

    public DbSet<Area> Areas => Set<Area>();
    public DbSet<DiningTable> DiningTables => Set<DiningTable>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<MenuItem> MenuItems => Set<MenuItem>();
    public DbSet<MenuPriceHistory> MenuPriceHistories => Set<MenuPriceHistory>();
    public DbSet<Staff> Staffs => Set<Staff>();
    public DbSet<ShiftSession> ShiftSessions => Set<ShiftSession>();
    public DbSet<Attendance> Attendances => Set<Attendance>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderDetail> OrderDetails => Set<OrderDetail>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<PaymentTransaction> PaymentTransactions => Set<PaymentTransaction>();
    public DbSet<KotTicket> KotTickets => Set<KotTicket>();
    public DbSet<Printer> Printers => Set<Printer>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(RestaurantDbContext).Assembly);
    }
}
