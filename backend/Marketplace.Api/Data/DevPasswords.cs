using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Marketplace.Api.Data;

// Development only. The seed users are created with a placeholder instead of a real hash.
// This gives each placeholder user a real hash of one known password, so the seed users can log in.
public static class DevPasswords
{
    public const string SeedUserPassword = "DevPassword123";
    private const string Placeholder = "DEV-ONLY-NOT-A-REAL-HASH";

    public static async Task ApplyAsync(MarketplaceDbContext db, IPasswordHasher<User> hasher)
    {
        var users = await db.Users.Where(u => u.PasswordHash == Placeholder).ToListAsync();
        if (users.Count == 0)
            return;

        foreach (var user in users)
            user.PasswordHash = hasher.HashPassword(user, SeedUserPassword);

        await db.SaveChangesAsync();
    }
}