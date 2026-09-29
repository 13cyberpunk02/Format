using System.Security.Claims;
using Format.Auth.Api.Data;
using Format.Auth.Api.Users;
using Format.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;
using UserOptions = Microsoft.AspNetCore.Identity.UserOptions;

namespace Format.Auth.Api.Endpoints;

public sealed record AdminUserDto(
    Guid Id,
    string Email,
    string DisplayName,
    UserRole Role,
    UserStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastLoginAt)
{
    public static AdminUserDto From(User u) =>
        new(u.Id, u.Email, u.DisplayName, u.Role, u.Status, u.CreatedAt, u.LastLoginAt);
}

public sealed record CreateUserRequest(string? Email, string? DisplayName, string? Password, UserRole? Role);
public sealed record UpdateUserRequest(string? DisplayName, UserRole? Role, UserStatus? Status);
public sealed record SetPasswordRequest(string? NewPassword);

public static class UserEndpoints
{
    public static IEndpointRouteBuilder MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/auth/users").RequireAuthorization(FormatAuthentication.AdminPolicy);

        group.MapGet("/", List);
        group.MapPost("/", Create);
        group.MapPatch("/{id:guid}", Update);
        group.MapPost("/{id:guid}/password", SetPassword);

        return app;
    }

    private static async Task<IResult> List(AuthDbContext db, CancellationToken ct)
    {
        var users = await db.Users
            .AsNoTracking()
            .OrderBy(u => u.DisplayName)
            .ToListAsync(ct);

        return Results.Ok(users.Select(AdminUserDto.From).ToList());
    }

    private static async Task<IResult> Create(
        CreateUserRequest request,
        AuthDbContext db,
        IPasswordHasher<User> hasher,
        IOptions<UserPolicyOptions> userOptions,
        TimeProvider time,
        CancellationToken ct)
    {
        var email = User.NormalizeEmail(request.Email ?? "");

        var error = UserRules.ValidateEmail(email, userOptions.Value)
            ?? UserRules.ValidateDisplayName(request.DisplayName)
            ?? UserRules.ValidatePassword(request.Password, email);

        if (error is not null)
            return BadRequest(error);

        if (await db.Users.AnyAsync(u => u.Email == email, ct))
            return EmailTaken();

        var now = time.GetUtcNow();
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            DisplayName = request.DisplayName!.Trim(),
            Role = request.Role ?? UserRole.User,
            Status = UserStatus.Active, // создан администратором - активация не нужна
            CreatedAt = now,
            ActivatedAt = now,
        };
        user.PasswordHash = hasher.HashPassword(user, request.Password!);

        db.Users.Add(user);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Кто-то создал пользователя с этой почтой между нашей проверкой и сохранением
            return EmailTaken();
        }

        return Results.Created($"/auth/users/{user.Id}", AdminUserDto.From(user));
    }

    private static async Task<IResult> Update(
        Guid id,
        UpdateUserRequest request,
        ClaimsPrincipal principal,
        AuthDbContext db,
        TimeProvider time,
        CancellationToken ct)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id, ct);
        if (user is null)
            return Results.NotFound();

        // Защита от того, чтобы единственный администратор случайно лишил себя доступа
        var isSelf = id == principal.GetUserId();
        if (isSelf && (request.Role is { } r && r != UserRole.Admin || request.Status is { } s && s != UserStatus.Active))
            return BadRequest("Нельзя снять с себя роль администратора или заблокировать себя.");

        if (request.DisplayName is not null)
        {
            var error = UserRules.ValidateDisplayName(request.DisplayName);
            if (error is not null)
                return BadRequest(error);

            user.DisplayName = request.DisplayName.Trim();
        }

        if (request.Role is { } role)
            user.Role = role;

        if (request.Status is { } status)
        {
            // Ручная активация администратором - пригодится, когда появится регистрация
            if (status == UserStatus.Active && user.ActivatedAt is null)
                user.ActivatedAt = time.GetUtcNow();

            user.Status = status;
        }

        await db.SaveChangesAsync(ct);
        return Results.Ok(AdminUserDto.From(user));
    }

    private static async Task<IResult> SetPassword(
        Guid id,
        SetPasswordRequest request,
        AuthDbContext db,
        IPasswordHasher<User> hasher,
        CancellationToken ct)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id, ct);
        if (user is null)
            return Results.NotFound();

        var error = UserRules.ValidatePassword(request.NewPassword, user.Email);
        if (error is not null)
            return BadRequest(error);

        user.PasswordHash = hasher.HashPassword(user, request.NewPassword!);
        await db.SaveChangesAsync(ct);

        return Results.NoContent();
    }

    private static IResult BadRequest(string title) =>
        Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: title);

    private static IResult EmailTaken() =>
        Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "Пользователь с такой почтой уже существует.");
}