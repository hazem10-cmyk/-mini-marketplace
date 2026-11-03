using Microsoft.EntityFrameworkCore;

namespace Marketplace.Api.Data;

public class MarketplaceDbContext(DbContextOptions<MarketplaceDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Service> Services => Set<Service>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<Review> Reviews => Set<Review>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(e =>
        {
            e.Property(x => x.Email).HasMaxLength(254);
            e.HasIndex(x => x.Email).IsUnique();
            e.Property(x => x.PasswordHash).HasMaxLength(255);
            e.Property(x => x.DisplayName).HasMaxLength(50);
            e.Property(x => x.Role).HasConversion<string>().HasMaxLength(20);
        });

        modelBuilder.Entity<Service>(e =>
        {
            e.Property(x => x.Title).HasMaxLength(80);
            e.Property(x => x.Description).HasMaxLength(2000);
            e.Property(x => x.Price).HasPrecision(10, 2);
            e.HasIndex(x => new { x.IsActive, x.Price });
            e.ToTable(t =>
            {
                t.HasCheckConstraint("CK_Services_Price", "Price > 0");
                t.HasCheckConstraint("CK_Services_DeliveryDays", "DeliveryDays BETWEEN 1 AND 60");
            });
            e.HasOne(x => x.Seller)
                .WithMany(u => u.Services)
                .HasForeignKey(x => x.SellerId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Order>(e =>
        {
            e.Property(x => x.PriceAtOrder).HasPrecision(10, 2);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.HasOne(x => x.Service)
                .WithMany(s => s.Orders)
                .HasForeignKey(x => x.ServiceId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Buyer)
                .WithMany(u => u.PlacedOrders)
                .HasForeignKey(x => x.BuyerId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Review>(e =>
        {
            e.Property(x => x.Comment).HasMaxLength(1000);
            e.ToTable(t => t.HasCheckConstraint("CK_Reviews_Rating", "Rating BETWEEN 1 AND 5"));
            e.HasOne(x => x.Order)
                .WithOne(o => o.Review)
                .HasForeignKey<Review>(x => x.OrderId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}