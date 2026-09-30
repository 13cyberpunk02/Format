using Microsoft.EntityFrameworkCore;

namespace Format.Auth.Api.Data;

public sealed class AuthDbContext(DbContextOptions<AuthDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var user = modelBuilder.Entity<User>();

        user.HasKey(u => u.Id);
        user.HasIndex(u => u.Email).IsUnique();

        user.Property(u => u.Email).HasMaxLength(254);
        user.Property(u => u.DisplayName).HasMaxLength(200);
        user.Property(u => u.Department).HasMaxLength(200);
        user.Property(u => u.PasswordHash).HasMaxLength(512);

        // Перечисления храним строками: в базе видно "Admin", а не 1
        user.Property(u => u.Role).HasConversion<string>().HasMaxLength(32);
        user.Property(u => u.Status).HasConversion<string>().HasMaxLength(32);
        
        var refresh = modelBuilder.Entity<RefreshToken>();

        refresh.HasKey(t => t.Id);
        refresh.Property(t => t.TokenHash).HasMaxLength(64);
        refresh.HasIndex(t => t.TokenHash).IsUnique();
        refresh.HasIndex(t => t.UserId);
        refresh.HasIndex(t => t.FamilyId);

        refresh.HasOne<User>()
            .WithMany()
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}