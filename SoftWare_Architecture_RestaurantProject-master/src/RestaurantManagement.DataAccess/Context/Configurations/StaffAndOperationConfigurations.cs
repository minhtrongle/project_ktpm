using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RestaurantManagement.DataAccess.Entities;

namespace RestaurantManagement.DataAccess.Context.Configurations;

public class StaffConfiguration : IEntityTypeConfiguration<Staff>
{
    public void Configure(EntityTypeBuilder<Staff> builder)
    {
        builder.HasKey(s => s.Id);
        builder.HasIndex(s => s.StaffCode).IsUnique();
        builder.HasIndex(s => s.Username).IsUnique();
        builder.HasIndex(s => s.CitizenId).IsUnique();
        builder.Property(s => s.StaffCode).HasMaxLength(20).IsRequired();
        builder.Property(s => s.FullName).HasMaxLength(100).IsRequired();
        builder.Property(s => s.Username).HasMaxLength(50).IsRequired();
        builder.Property(s => s.PasswordHash).IsRequired();
        builder.Property(s => s.PhoneNumber).HasMaxLength(20);
        builder.Property(s => s.CitizenId).HasMaxLength(20).IsRequired();
    }
}

public class ShiftSessionConfiguration : IEntityTypeConfiguration<ShiftSession>
{
    public void Configure(EntityTypeBuilder<ShiftSession> builder)
    {
        builder.HasKey(s => s.Id);
        builder.HasIndex(s => s.ShiftCode).IsUnique();
        builder.Property(s => s.ShiftCode).HasMaxLength(50).IsRequired();
        builder.Property(s => s.CounterCode).HasMaxLength(20).IsRequired();
        builder.Property(s => s.InitialCash).HasPrecision(18, 2);
        builder.Property(s => s.TheoreticalCash).HasPrecision(18, 2);
        builder.Property(s => s.ActualCash).HasPrecision(18, 2);
        builder.Property(s => s.Difference).HasPrecision(18, 2);

        builder.HasOne(s => s.Staff)
            .WithMany(st => st.ShiftSessions)
            .HasForeignKey(s => s.StaffId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.ApprovedBy)
            .WithMany()
            .HasForeignKey(s => s.ApprovedById)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.HasKey(o => o.Id);
        builder.HasIndex(o => o.OrderCode).IsUnique();
        builder.Property(o => o.OrderCode).HasMaxLength(50).IsRequired();
        builder.Property(o => o.TotalAmount).HasPrecision(18, 2);
        builder.Property(o => o.Xmin).IsRowVersion();

        builder.HasOne(o => o.Table)
            .WithMany(t => t.Orders)
            .HasForeignKey(o => o.TableId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(o => o.Booking)
            .WithMany(b => b.Orders)
            .HasForeignKey(o => o.BookingId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(o => o.CreatedBy)
            .WithMany(s => s.CreatedOrders)
            .HasForeignKey(o => o.CreatedById)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class OrderDetailConfiguration : IEntityTypeConfiguration<OrderDetail>
{
    public void Configure(EntityTypeBuilder<OrderDetail> builder)
    {
        builder.HasKey(d => d.Id);
        builder.Property(d => d.UnitPrice).HasPrecision(18, 2);
        builder.Property(d => d.SubTotal).HasPrecision(18, 2);

        builder.HasOne(d => d.Order)
            .WithMany(o => o.OrderDetails)
            .HasForeignKey(d => d.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(d => d.MenuItem)
            .WithMany(m => m.OrderDetails)
            .HasForeignKey(d => d.MenuItemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(d => d.ApprovedBy)
            .WithMany()
            .HasForeignKey(d => d.ApprovedById)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
    public void Configure(EntityTypeBuilder<Invoice> builder)
    {
        builder.HasKey(i => i.Id);
        builder.HasIndex(i => i.InvoiceCode).IsUnique();
        builder.Property(i => i.InvoiceCode).HasMaxLength(50).IsRequired();
        builder.Property(i => i.TotalAmount).HasPrecision(18, 2);
        builder.Property(i => i.SurchargeAmount).HasPrecision(18, 2);
        builder.Property(i => i.VatAmount).HasPrecision(18, 2);
        builder.Property(i => i.DiscountAmount).HasPrecision(18, 2);
        builder.Property(i => i.ManualDiscountAmount).HasPrecision(18, 2);
        builder.Property(i => i.DepositDeducted).HasPrecision(18, 2);
        builder.Property(i => i.FinalAmount).HasPrecision(18, 2);
        builder.Property(i => i.CashReceived).HasPrecision(18, 2);
        builder.Property(i => i.ChangeAmount).HasPrecision(18, 2);

        builder.HasOne(i => i.Order)
            .WithMany(o => o.Invoices)
            .HasForeignKey(i => i.OrderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(i => i.Booking)
            .WithMany(b => b.Invoices)
            .HasForeignKey(i => i.BookingId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(i => i.Customer)
            .WithMany(c => c.Invoices)
            .HasForeignKey(i => i.CustomerId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(i => i.Staff)
            .WithMany(s => s.CreatedInvoices)
            .HasForeignKey(i => i.StaffId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(i => i.ApprovedBy)
            .WithMany()
            .HasForeignKey(i => i.ApprovedById)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class PaymentTransactionConfiguration : IEntityTypeConfiguration<PaymentTransaction>
{
    public void Configure(EntityTypeBuilder<PaymentTransaction> builder)
    {
        builder.HasKey(p => p.Id);
        builder.HasIndex(p => p.TxnRef).IsUnique();
        builder.Property(p => p.TxnRef).HasMaxLength(100).IsRequired();
        builder.Property(p => p.Amount).HasPrecision(18, 2);
        builder.Property(p => p.Provider).HasMaxLength(50);

        builder.HasOne(p => p.Booking)
            .WithMany(b => b.PaymentTransactions)
            .HasForeignKey(p => p.BookingId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class KotTicketConfiguration : IEntityTypeConfiguration<KotTicket>
{
    public void Configure(EntityTypeBuilder<KotTicket> builder)
    {
        builder.HasKey(k => k.Id);
        builder.HasIndex(k => k.TicketCode).IsUnique();
        builder.HasIndex(k => k.RequestId).IsUnique();
        builder.Property(k => k.TicketCode).HasMaxLength(50).IsRequired();
        builder.Property(k => k.RequestId).HasMaxLength(100).IsRequired();

        builder.HasOne(k => k.Order)
            .WithMany(o => o.KotTickets)
            .HasForeignKey(k => k.OrderId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class AttendanceConfiguration : IEntityTypeConfiguration<Attendance>
{
    public void Configure(EntityTypeBuilder<Attendance> builder)
    {
        builder.HasKey(a => a.Id);

        builder.HasOne(a => a.Staff)
            .WithMany(s => s.Attendances)
            .HasForeignKey(a => a.StaffId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(a => a.ApprovedBy)
            .WithMany()
            .HasForeignKey(a => a.ApprovedById)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

