using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Format.Auth.Api.Data;

public sealed class BootstrapAdminOptions
{
    public string Email { get; set; } = "";
    public string Password { get; set; } = "";
    public string DisplayName { get; set; } = "Администратор";
}

/// <summary>Создаёт первого администратора, если в базе ещё нет ни одного пользователя.</summary>
public static class AdminSeeder
{
    public static async Task SeedAsync(IServiceProvider services, CancellationToken ct = default)
    {
        var db = services.GetRequiredService<AuthDbContext>();
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("AdminSeeder");

        if (await db.Users.AnyAsync(ct))
            return;

        var options = services.GetRequiredService<IOptions<BootstrapAdminOptions>>().Value;

        if (string.IsNullOrWhiteSpace(options.Email) || string.IsNullOrWhiteSpace(options.Password))
        {
            logger.LogWarning("В базе нет пользователей, а BootstrapAdmin не настроен - войти в систему будет некому.");
            return;
        }

        var hasher = services.GetRequiredService<IPasswordHasher<User>>();
        var now = DateTimeOffset.UtcNow;

        var admin = new User
        {
            Id = Guid.NewGuid(),
            Email = User.NormalizeEmail(options.Email),
            DisplayName = options.DisplayName,
            Role = UserRole.Admin,
            Status = UserStatus.Active,
            CreatedAt = now,
            ActivatedAt = now,
        };
        admin.PasswordHash = hasher.HashPassword(admin, options.Password);

        db.Users.Add(admin);
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Создан первый администратор {Email}", admin.Email);
    }
}