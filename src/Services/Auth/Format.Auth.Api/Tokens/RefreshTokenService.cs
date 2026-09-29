using System.Security.Cryptography;
using System.Text;
using Format.Auth.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Format.Auth.Api.Tokens;

public sealed class RefreshTokenOptions
{
    public int LifetimeDays { get; set; } = 14;
    public string CookieName { get; set; } = "format_refresh";

    /// <summary>Путь, как его видит браузер. За nginx - "/api/auth", напрямую в разработке - "/auth".</summary>
    public string CookiePath { get; set; } = "/api/auth";

    /// <summary>Только HTTPS. В разработке по HTTP - false.</summary>
    public bool CookieSecure { get; set; } = true;
}

public sealed record IssuedRefreshToken(string Value, DateTimeOffset ExpiresAt, Guid FamilyId);

public sealed record RotationResult(User User, IssuedRefreshToken Token);

public sealed class RefreshTokenService(
    AuthDbContext db,
    IOptions<RefreshTokenOptions> options,
    TimeProvider time,
    ILogger<RefreshTokenService> logger)
{
    /// <summary>Окно, в котором повторное предъявление считается гонкой вкладок, а не кражей.</summary>
    private static readonly TimeSpan ReuseGrace = TimeSpan.FromSeconds(30);

    /// <summary>Новый токен. familyId = null - новая сессия (вход).</summary>
    public async Task<IssuedRefreshToken> IssueAsync(Guid userId, Guid? familyId, CancellationToken ct)
    {
        var now = time.GetUtcNow();

        // Попутно убираем давно истёкшие токены пользователя, чтобы таблица не росла бесконечно
        await db.RefreshTokens
            .Where(t => t.UserId == userId && t.ExpiresAt < now)
            .ExecuteDeleteAsync(ct);

        var (entity, value) = Create(userId, familyId ?? Guid.NewGuid(), now);
        db.RefreshTokens.Add(entity);
        await db.SaveChangesAsync(ct);

        return new IssuedRefreshToken(value, entity.ExpiresAt, entity.FamilyId);
    }

    /// <summary>Обменять токен на новый. null - токен недействителен, нужно войти заново.</summary>
    public async Task<RotationResult?> RotateAsync(string value, CancellationToken ct)
    {
        var now = time.GetUtcNow();
        var hash = Hash(value);

        var token = await db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, ct);
        if (token is null || token.ExpiresAt <= now)
            return null;

        if (token.RevokedAt is { } revokedAt)
        {
            var isTabRace = token.ReplacedById is not null && now - revokedAt <= ReuseGrace;
            if (!isTabRace)
            {
                logger.LogWarning(
                    "Повторно предъявлен отозванный refresh-токен пользователя {UserId}. Сессия {FamilyId} закрыта.",
                    token.UserId, token.FamilyId);

                await RevokeFamilyAsync(token.FamilyId, ct);
                return null;
            }
        }

        // Статус проверяется при каждом обмене: заблокированный не продлит сессию
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == token.UserId, ct);
        if (user is null || user.Status != UserStatus.Active)
        {
            await RevokeFamilyAsync(token.FamilyId, ct);
            return null;
        }

        var (next, nextValue) = Create(user.Id, token.FamilyId, now);
        db.RefreshTokens.Add(next);

        if (token.RevokedAt is null)
        {
            token.RevokedAt = now;
            token.ReplacedById = next.Id;
        }

        await db.SaveChangesAsync(ct);

        return new RotationResult(user, new IssuedRefreshToken(nextValue, next.ExpiresAt, next.FamilyId));
    }

    /// <summary>Семейство токена по его значению; null - токен не найден.</summary>
    public async Task<Guid?> FindFamilyAsync(string value, CancellationToken ct)
    {
        var hash = Hash(value);
        return await db.RefreshTokens
            .Where(t => t.TokenHash == hash)
            .Select(t => (Guid?)t.FamilyId)
            .FirstOrDefaultAsync(ct);
    }

    /// <summary>Закрыть одну сессию.</summary>
    public Task RevokeFamilyAsync(Guid familyId, CancellationToken ct) =>
        Revoke(db.RefreshTokens.Where(t => t.FamilyId == familyId), ct);

    /// <summary>Закрыть все сессии пользователя, кроме, возможно, текущей.</summary>
    public Task RevokeAllAsync(Guid userId, Guid? exceptFamilyId, CancellationToken ct) =>
        Revoke(db.RefreshTokens.Where(t => t.UserId == userId && t.FamilyId != exceptFamilyId), ct);

    private Task Revoke(IQueryable<RefreshToken> tokens, CancellationToken ct)
    {
        var now = time.GetUtcNow();
        return tokens
            .Where(t => t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, (DateTimeOffset?)now), ct);
    }

    private (RefreshToken Entity, string Value) Create(Guid userId, Guid familyId, DateTimeOffset now)
    {
        var value = Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(32));

        var entity = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            FamilyId = familyId,
            TokenHash = Hash(value),
            CreatedAt = now,
            ExpiresAt = now.AddDays(options.Value.LifetimeDays),
        };

        return (entity, value);
    }

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}