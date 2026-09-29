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
        group.MapPost("/refresh", Refresh).AllowAnonymous();
        group.MapPost("/logout", Logout).AllowAnonymous();
        group.MapPost("/me/logout-all", LogoutEverywhere);
        group.MapGet("/.well-known/openid-configuration", Discovery).AllowAnonymous();
        group.MapGet("/.well-known/jwks.json", Jwks).AllowAnonymous();

        group.MapGet("/me", GetMe);
        group.MapPost("/me/password", ChangeOwnPassword).RequireRateLimiting("login");

        return app;
    }

    private static async Task<IResult> Login(
        LoginRequest request,
        HttpResponse response,
        RefreshTokenService refreshTokens,
        IOptions<RefreshTokenOptions> refreshOptions,
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

        var refresh = await refreshTokens.IssueAsync(user.Id, familyId: null, ct);
        SetRefreshCookie(response, refresh, refreshOptions.Value);

        var token = tokens.CreateAccessToken(user);
        return Results.Ok(new LoginResponse(token.Token, token.ExpiresAt, UserDto.From(user)));
    }

    private static async Task<IResult> Refresh(
        HttpContext http,
        RefreshTokenService refreshTokens,
        TokenService tokens,
        IOptions<RefreshTokenOptions> options,
        CancellationToken ct)
    {
        var o = options.Value;

        if (!http.Request.Cookies.TryGetValue(o.CookieName, out var value) || string.IsNullOrEmpty(value))
            return SessionExpired();

        var result = await refreshTokens.RotateAsync(value, ct);
        if (result is null)
        {
            ClearRefreshCookie(http.Response, o);
            return SessionExpired();
        }

        SetRefreshCookie(http.Response, result.Token, o);

        // Токен собирается заново из базы: новое ФИО или роль вступают в силу при продлении
        var access = tokens.CreateAccessToken(result.User);
        return Results.Ok(new LoginResponse(access.Token, access.ExpiresAt, UserDto.From(result.User)));
    }

    private static async Task<IResult> Logout(
        HttpContext http,
        RefreshTokenService refreshTokens,
        IOptions<RefreshTokenOptions> options,
        CancellationToken ct)
    {
        var o = options.Value;

        if (http.Request.Cookies.TryGetValue(o.CookieName, out var value) &&
            await refreshTokens.FindFamilyAsync(value, ct) is { } familyId)
        {
            await refreshTokens.RevokeFamilyAsync(familyId, ct);
        }

        ClearRefreshCookie(http.Response, o);
        return Results.NoContent();
    }

    private static async Task<IResult> LogoutEverywhere(
        ClaimsPrincipal principal,
        HttpResponse response,
        RefreshTokenService refreshTokens,
        IOptions<RefreshTokenOptions> options,
        CancellationToken ct)
    {
        await refreshTokens.RevokeAllAsync(principal.GetUserId(), exceptFamilyId: null, ct);
        ClearRefreshCookie(response, options.Value);
        return Results.NoContent();
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
        HttpRequest httpRequest,
        ChangePasswordRequest request,
        RefreshTokenService refreshTokens,
        IOptions<RefreshTokenOptions> refreshOptions,
        ClaimsPrincipal principal,
        AuthDbContext db,
        IPasswordHasher<User> hasher,
        CancellationToken ct)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == principal.GetUserId(), ct);
        if (user is null)
            return Results.NotFound();

        if (string.IsNullOrEmpty(request.CurrentPassword) ||
            hasher.VerifyHashedPassword(user, user.PasswordHash, request.CurrentPassword) ==
            PasswordVerificationResult.Failed)
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest,
                title: "Текущий пароль указан неверно.");

        var error = UserRules.ValidatePassword(request.NewPassword, user.Email);
        if (error is not null)
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: error);

        user.PasswordHash = hasher.HashPassword(user, request.NewPassword!);
        await db.SaveChangesAsync(ct);

        // Закрываем все сессии, кроме текущей: пользователь остаётся в системе на этом устройстве
        Guid? currentFamily = null;
        if (httpRequest.Cookies.TryGetValue(refreshOptions.Value.CookieName, out var cookie))
            currentFamily = await refreshTokens.FindFamilyAsync(cookie, ct);

        await refreshTokens.RevokeAllAsync(user.Id, currentFamily, ct);
        
        return Results.NoContent();
    }

    private static void SetRefreshCookie(HttpResponse response, IssuedRefreshToken token,
        RefreshTokenOptions options) =>
        response.Cookies.Append(options.CookieName, token.Value, new CookieOptions
        {
            HttpOnly = true,
            Secure = options.CookieSecure,
            SameSite = SameSiteMode.Strict,
            Path = options.CookiePath,
            Expires = token.ExpiresAt,
            IsEssential = true,
        });

    private static void ClearRefreshCookie(HttpResponse response, RefreshTokenOptions options) =>
        response.Cookies.Delete(options.CookieName, new CookieOptions
        {
            HttpOnly = true,
            Secure = options.CookieSecure,
            SameSite = SameSiteMode.Strict,
            Path = options.CookiePath,
        });

    private static IResult SessionExpired() =>
        Results.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Сессия истекла, войдите заново.");
}