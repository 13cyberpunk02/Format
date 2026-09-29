using System.Security.Claims;
using Format.Auth.Api.Data;
using Format.Auth.Api.Tokens;
using Format.Auth.Api.Users;
using Format.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Format.Auth.Api.Endpoints;

public sealed record LoginRequest(string? Email, string? Password);
public sealed record ChangePasswordRequest(string? CurrentPassword, string? NewPassword);

public sealed record UserDto(Guid Id, string Email, string DisplayName, string Role)
{
    public static UserDto From(User u) => new(u.Id, u.Email, u.DisplayName, u.Role.ToString());
}

public sealed record LoginResponse(string AccessToken, DateTimeOffset ExpiresAt, UserDto User);

public static class AuthEndpoints
{
    /// <summary>Хэш случайного пароля - для выравнивания времени ответа, когда пользователь не найден.</summary>
    private static readonly string DummyHash =
        new PasswordHasher<User>().HashPassword(new User { Email = "", DisplayName = "" }, Guid.NewGuid().ToString());

    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/auth");

        group.MapPost("/login", Login).AllowAnonymous().RequireRateLimiting("login");
        group.MapGet("/.well-known/openid-configuration", Discovery).AllowAnonymous();
        group.MapGet("/.well-known/jwks.json", Jwks).AllowAnonymous();

        group.MapGet("/me", GetMe);
        group.MapPost("/me/password", ChangeOwnPassword).RequireRateLimiting("login");

        return app;
    }

    private static async Task<IResult> Login(
        LoginRequest request,
        AuthDbContext db,
        IPasswordHasher<User> hasher,
        TokenService tokens,
        TimeProvider time,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrEmpty(request.Password))
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Укажите почту и пароль");

        var email = User.NormalizeEmail(request.Email);
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);

        if (user is null)
        {
            // Проверяем пароль «вхолостую», чтобы ответ занял столько же времени, сколько для существующего
            hasher.VerifyHashedPassword(user!, DummyHash, request.Password);
            return InvalidCredentials();
        }

        var check = hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (check == PasswordVerificationResult.Failed)
            return InvalidCredentials();

        // Статус проверяем только после пароля: иначе без пароля можно узнать, что учётная запись существует
        if (user.Status == UserStatus.PendingActivation)
            return Results.Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: "Учётная запись не активирована",
                detail: "Перейдите по ссылке из письма, которое пришло на вашу почту.");

        if (user.Status == UserStatus.Disabled)
            return Results.Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: "Учётная запись заблокирована",
                detail: "Обратитесь к администратору.");

        // Алгоритм хэширования обновился (например, стало больше итераций) - перехэшируем по новым правилам
        if (check == PasswordVerificationResult.SuccessRehashNeeded)
            user.PasswordHash = hasher.HashPassword(user, request.Password);

        user.LastLoginAt = time.GetUtcNow();
        await db.SaveChangesAsync(ct);

        var token = tokens.CreateAccessToken(user);
        return Results.Ok(new LoginResponse(token.Token, token.ExpiresAt, UserDto.From(user)));
    }
    
    /// <summary>Документ обнаружения: кто издатель и где лежат ключи.</summary>
    private static IResult Discovery(HttpRequest request, IOptions<JwtOptions> options) =>
        Results.Ok(new
        {
            issuer = options.Value.Issuer,
            jwks_uri = $"{request.Scheme}://{request.Host}{request.PathBase}/auth/.well-known/jwks.json",
        });

    /// <summary>Открытые ключи для проверки подписи токенов.</summary>
    private static IResult Jwks(JwtSigningKey key) =>
        Results.Ok(new { keys = new[] { key.PublicJwk } });
    
    private static IResult InvalidCredentials() =>
        Results.Problem(
            statusCode: StatusCodes.Status401Unauthorized,
            title: "Неверная почта или пароль");
    
    private static async Task<IResult> GetMe(ClaimsPrincipal principal, AuthDbContext db, CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == principal.GetUserId(), ct);

        return user is null ? Results.NotFound() : Results.Ok(UserDto.From(user));
    }

    private static async Task<IResult> ChangeOwnPassword(
        ChangePasswordRequest request,
        ClaimsPrincipal principal,
        AuthDbContext db,
        IPasswordHasher<User> hasher,
        CancellationToken ct)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == principal.GetUserId(), ct);
        if (user is null)
            return Results.NotFound();

        if (string.IsNullOrEmpty(request.CurrentPassword) ||
            hasher.VerifyHashedPassword(user, user.PasswordHash, request.CurrentPassword) == PasswordVerificationResult.Failed)
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Текущий пароль указан неверно.");

        var error = UserRules.ValidatePassword(request.NewPassword, user.Email);
        if (error is not null)
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: error);

        user.PasswordHash = hasher.HashPassword(user, request.NewPassword!);
        await db.SaveChangesAsync(ct);

        return Results.NoContent();
    }
}