using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RestaurantManagement.DataAccess.Entities;

namespace RestaurantManagement.DataAccess.Context.Configurations;

public class DiningTableConfiguration : IEntityTypeConfiguration<DiningTable>
{
    public void Configure(EntityTypeBuilder<DiningTable> builder)
    {
        builder.HasKey(t => t.Id);
        builder.HasIndex(t => t.TableNumber).IsUnique();
        builder.Property(t => t.TableNumber).HasMaxLength(20).IsRequired();
        builder.Property(t => t.Capacity).IsRequired();
        builder.Property(t => t.Xmin).IsRowVersion();

        builder.HasOne(t => t.Area)
            .WithMany(a => a.Tables)
            .HasForeignKey(t => t.AreaId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class AreaConfiguration : IEntityTypeConfiguration<Area>
{
    public void Configure(EntityTypeBuilder<Area> builder)
    {
        builder.HasKey(a => a.Id);
        builder.Property(a => a.AreaName).HasMaxLength(100).IsRequired();
        builder.Property(a => a.Description).HasMaxLength(255);
    }
}

public class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.HasKey(c => c.Id);
        builder.HasIndex(c => c.PhoneNumber).IsUnique();
        builder.Property(c => c.CustomerName).HasMaxLength(100).IsRequired();
        builder.Property(c => c.PhoneNumber).HasMaxLength(20).IsRequired();
        builder.Property(c => c.Email).HasMaxLength(100);
    }
}

public class BookingConfiguration : IEntityTypeConfiguration<Booking>
{
    public void Configure(EntityTypeBuilder<Booking> builder)
    {
        builder.HasKey(b => b.Id);
        builder.HasIndex(b => b.BookingCode).IsUnique();
        builder.Property(b => b.BookingCode).HasMaxLength(50).IsRequired();
        builder.Property(b => b.CustomerName).HasMaxLength(100).IsRequired();
        builder.Property(b => b.PhoneNumber).HasMaxLength(20).IsRequired();
        builder.Property(b => b.DepositAmount).HasPrecision(18, 2);

        builder.HasOne(b => b.Table)
            .WithMany(t => t.Bookings)
            .HasForeignKey(b => b.TableId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(b => b.Customer)
            .WithMany(c => c.Bookings)
            .HasForeignKey(b => b.CustomerId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

public class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.HasKey(c => c.Id);
        builder.HasIndex(c => c.CategoryName).IsUnique();
        builder.Property(c => c.CategoryName).HasMaxLength(100).IsRequired();
    }
}

public class MenuItemConfiguration : IEntityTypeConfiguration<MenuItem>
{
    public void Configure(EntityTypeBuilder<MenuItem> builder)
    {
        builder.HasKey(m => m.Id);
        builder.HasIndex(m => m.ItemCode).IsUnique();
        builder.HasIndex(m => m.ItemName).IsUnique();
        builder.Property(m => m.ItemCode).HasMaxLength(20).IsRequired();
        builder.Property(m => m.ItemName).HasMaxLength(150).IsRequired();
        builder.Property(m => m.Price).HasPrecision(18, 2);
        builder.Property(m => m.Unit).HasMaxLength(50);

        builder.HasOne(m => m.Category)
            .WithMany(c => c.MenuItems)
            .HasForeignKey(m => m.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class MenuPriceHistoryConfiguration : IEntityTypeConfiguration<MenuPriceHistory>
{
    public void Configure(EntityTypeBuilder<MenuPriceHistory> builder)
    {
        builder.HasKey(p => p.Id);
        builder.Property(p => p.OldPrice).HasPrecision(18, 2);
        builder.Property(p => p.NewPrice).HasPrecision(18, 2);

        builder.HasOne(p => p.MenuItem)
            .WithMany(m => m.PriceHistories)
            .HasForeignKey(p => p.MenuItemId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(p => p.ChangedBy)
            .WithMany()
            .HasForeignKey(p => p.ChangedById)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
