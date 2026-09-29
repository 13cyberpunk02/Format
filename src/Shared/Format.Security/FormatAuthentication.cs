using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Format.Security;

public sealed class JwtValidationOptions
{
    /// <summary>Адрес документа обнаружения сервиса авторизации.</summary>
    public string MetadataAddress { get; set; } = "";
    public string Issuer { get; set; } = "format-auth";
    public string Audience { get; set; } = "format";
}

public static class FormatAuthentication
{
    public const string AdminRole = "Admin";
    public const string AdminPolicy = "Admin";

    /// <summary>Проверка JWT от сервиса авторизации. По умолчанию все эндпоинты требуют вход.</summary>
    public static IServiceCollection AddFormatAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var options = configuration.GetSection("Jwt").Get<JwtValidationOptions>()
            ?? throw new InvalidOperationException("Не задана секция Jwt в настройках.");

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(jwt =>
            {
                jwt.MetadataAddress = options.MetadataAddress;
                jwt.RequireHttpsMetadata = false;
                jwt.MapInboundClaims = false;

                jwt.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = options.Issuer,
                    ValidAudience = options.Audience,
                    NameClaimType = "name",
                    RoleClaimType = "role",
                    ClockSkew = TimeSpan.FromSeconds(30),
                };
            });

        services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())
            .AddPolicy(AdminPolicy, policy => policy.RequireRole(AdminRole));

        return services;
    }

    public static Guid GetUserId(this ClaimsPrincipal user) =>
        Guid.Parse(user.FindFirstValue("sub") ?? throw new InvalidOperationException("В токене нет sub."));

    public static string GetDisplayName(this ClaimsPrincipal user) =>
        user.FindFirstValue("name") ?? "";

    public static bool IsAdmin(this ClaimsPrincipal user) =>
        user.IsInRole(AdminRole);
}