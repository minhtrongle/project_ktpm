using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using RestaurantManagement.DataAccess.Context;
using RestaurantManagement.DataAccess.Entities;
using RestaurantManagement.DataAccess.Repositories;

namespace RestaurantManagement.DataAccess.UnitOfWork;

public interface IUnitOfWork : IDisposable
{
    ITableRepository Tables { get; }
    IBookingRepository Bookings { get; }
    IStaffRepository Staffs { get; }
    IRepository<Area> Areas { get; }
    IRepository<Category> Categories { get; }
    IRepository<MenuItem> MenuItems { get; }
    IRepository<MenuPriceHistory> MenuPriceHistories { get; }
    IRepository<Customer> Customers { get; }
    IRepository<Order> Orders { get; }
    IRepository<OrderDetail> OrderDetails { get; }
    IRepository<Invoice> Invoices { get; }
    IRepository<ShiftSession> ShiftSessions { get; }
    IRepository<Attendance> Attendances { get; }
    IRepository<PaymentTransaction> PaymentTransactions { get; }
    IRepository<KotTicket> KotTickets { get; }
    IRepository<Printer> Printers { get; }
    IRepository<AuditLog> AuditLogs { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    Task<T> ExecuteInTransactionAsync<T>(Func<Task<T>> action, CancellationToken cancellationToken = default);
    Task ExecuteInTransactionAsync(Func<Task> action, CancellationToken cancellationToken = default);
}

public class UnitOfWork : IUnitOfWork
{
    private readonly RestaurantDbContext _context;

    public UnitOfWork(
        RestaurantDbContext context,
        ITableRepository tables,
        IBookingRepository bookings,
        IStaffRepository staffs)
    {
        _context = context;
        Tables = tables;
        Bookings = bookings;
        Staffs = staffs;

        Areas = new Repository<Area>(_context);
        Categories = new Repository<Category>(_context);
        MenuItems = new Repository<MenuItem>(_context);
        MenuPriceHistories = new Repository<MenuPriceHistory>(_context);
        Customers = new Repository<Customer>(_context);
        Orders = new Repository<Order>(_context);
        OrderDetails = new Repository<OrderDetail>(_context);
        Invoices = new Repository<Invoice>(_context);
        ShiftSessions = new Repository<ShiftSession>(_context);
        Attendances = new Repository<Attendance>(_context);
        PaymentTransactions = new Repository<PaymentTransaction>(_context);
        KotTickets = new Repository<KotTicket>(_context);
        Printers = new Repository<Printer>(_context);
        AuditLogs = new Repository<AuditLog>(_context);
    }

    public ITableRepository Tables { get; }
    public IBookingRepository Bookings { get; }
    public IStaffRepository Staffs { get; }
    public IRepository<Area> Areas { get; }
    public IRepository<Category> Categories { get; }
    public IRepository<MenuItem> MenuItems { get; }
    public IRepository<MenuPriceHistory> MenuPriceHistories { get; }
    public IRepository<Customer> Customers { get; }
    public IRepository<Order> Orders { get; }
    public IRepository<OrderDetail> OrderDetails { get; }
    public IRepository<Invoice> Invoices { get; }
    public IRepository<ShiftSession> ShiftSessions { get; }
    public IRepository<Attendance> Attendances { get; }
    public IRepository<PaymentTransaction> PaymentTransactions { get; }
    public IRepository<KotTicket> KotTickets { get; }
    public IRepository<Printer> Printers { get; }
    public IRepository<AuditLog> AuditLogs { get; }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<T> ExecuteInTransactionAsync<T>(Func<Task<T>> action, CancellationToken cancellationToken = default)
    {
        var strategy = _context.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                var result = await action();
                await transaction.CommitAsync(cancellationToken);
                return result;
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        });
    }

    public async Task ExecuteInTransactionAsync(Func<Task> action, CancellationToken cancellationToken = default)
    {
        var strategy = _context.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                await action();
                await transaction.CommitAsync(cancellationToken);
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        });
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}
