using Microsoft.EntityFrameworkCore;

namespace Format.Auth.Api.Data;

public sealed class AuthDbContext(DbContextOptions<AuthDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var user = modelBuilder.Entity<User>();

        user.HasKey(u => u.Id);
        user.HasIndex(u => u.Email).IsUnique();

        user.Property(u => u.Email).HasMaxLength(254);
        user.Property(u => u.DisplayName).HasMaxLength(200);
        user.Property(u => u.PasswordHash).HasMaxLength(512);

        // Перечисления храним строками: в базе видно "Admin", а не 1
        user.Property(u => u.Role).HasConversion<string>().HasMaxLength(32);
        user.Property(u => u.Status).HasConversion<string>().HasMaxLength(32);
    }
}