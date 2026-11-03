using Microsoft.EntityFrameworkCore;

namespace Marketplace.Api.Data;

public static class DevSeeder
{
    public static async Task SeedAsync(MarketplaceDbContext db)
    {
        if (await db.Users.AnyAsync())
            return;

        var now = DateTime.UtcNow;
        const string fakeHash = "DEV-ONLY-NOT-A-REAL-HASH";

        var admin = new User { Email = "admin@marketplace.test", DisplayName = "Admin", Role = UserRole.Admin, PasswordHash = fakeHash };
        var sara = new User { Email = "sara@marketplace.test", DisplayName = "Sara", Role = UserRole.Seller, PasswordHash = fakeHash };
        var omar = new User { Email = "omar@marketplace.test", DisplayName = "Omar", Role = UserRole.Seller, PasswordHash = fakeHash };
        var ali = new User { Email = "ali@marketplace.test", DisplayName = "Ali", Role = UserRole.Buyer, PasswordHash = fakeHash };
        var maya = new User { Email = "maya@marketplace.test", DisplayName = "Maya", Role = UserRole.Buyer, PasswordHash = fakeHash };

        var logo = new Service { Seller = sara, Title = "Professional logo design", Description = "A clean, original logo with 2 revisions.", Price = 25m, DeliveryDays = 3 };
        var translation = new Service { Seller = omar, Title = "Article translation (up to 500 words)", Description = "English to Arabic translation by a native speaker.", Price = 10m, DeliveryDays = 2 };
        var landing = new Service { Seller = omar, Title = "Landing page development", Description = "A responsive one-page website built from your content.", Price = 60m, DeliveryDays = 7 };
        var banners = new Service { Seller = sara, Title = "Social media banner pack", Description = "Five banners sized for the main platforms.", Price = 15m, DeliveryDays = 2 };
        var proofreading = new Service { Seller = omar, Title = "Proofreading (up to 1000 words)", Description = "Spelling, grammar, and style fixes.", Price = 8m, DeliveryDays = 1 };
        var cards = new Service { Seller = sara, Title = "Business card design", Description = "Front and back, print-ready PDF.", Price = 12m, DeliveryDays = 2 };
        var flyer = new Service { Seller = sara, Title = "Flyer design (paused)", Description = "A hidden service, used to test the IsActive filter.", Price = 20m, DeliveryDays = 3, IsActive = false };

        // The logo order was placed when the price was 20. The service costs 25 now, but the order keeps 20 (decision D3).
        var order1 = new Order { Service = logo, Buyer = ali, PriceAtOrder = 20m, Status = OrderStatus.Completed, CreatedAt = now.AddDays(-14), UpdatedAt = now.AddDays(-10) };
        var order2 = new Order { Service = translation, Buyer = maya, PriceAtOrder = 10m, Status = OrderStatus.Completed, CreatedAt = now.AddDays(-12), UpdatedAt = now.AddDays(-9) };
        var order3 = new Order { Service = landing, Buyer = ali, PriceAtOrder = 60m, Status = OrderStatus.InProgress, CreatedAt = now.AddDays(-5), UpdatedAt = now.AddDays(-4) };
        var order4 = new Order { Service = banners, Buyer = maya, PriceAtOrder = 15m, Status = OrderStatus.Delivered, CreatedAt = now.AddDays(-4), UpdatedAt = now.AddDays(-1) };
        var order5 = new Order { Service = proofreading, Buyer = ali, PriceAtOrder = 8m, Status = OrderStatus.Pending, CreatedAt = now.AddDays(-1), UpdatedAt = now.AddDays(-1) };
        var order6 = new Order { Service = cards, Buyer = maya, PriceAtOrder = 12m, Status = OrderStatus.Cancelled, CreatedAt = now.AddDays(-3), UpdatedAt = now.AddDays(-2) };

        var review1 = new Review { Order = order1, Rating = 5, Comment = "Great work and fast delivery!", CreatedAt = now.AddDays(-9) };
        var review2 = new Review { Order = order2, Rating = 4, Comment = "Good translation, a little late.", CreatedAt = now.AddDays(-8) };

        db.Users.AddRange(admin, sara, omar, ali, maya);
        db.Services.AddRange(logo, translation, landing, banners, proofreading, cards, flyer);
        db.Orders.AddRange(order1, order2, order3, order4, order5, order6);
        db.Reviews.AddRange(review1, review2);

        await db.SaveChangesAsync();
    }
}