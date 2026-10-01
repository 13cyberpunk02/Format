using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Format.Auth.Api.Data;
using Format.Auth.Api.Endpoints;
using Format.Auth.Api.Tokens;
using Format.Auth.Api.Users;
using Format.Security;
using Format.ServiceDefaults;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.AddFormatDefaults();

builder.Services.AddOpenApi();

builder.Services.AddFormatAuthentication(builder.Configuration);
builder.Services
    .AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<JwtSigningKey>((jwt, key) => jwt.TokenValidationParameters.IssuerSigningKey = key.SecurityKey);

builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.Configure<UserPolicyOptions>(builder.Configuration.GetSection("Users"));

builder.Services.AddDbContext<AuthDbContext>(options =>
    options
        .UseNpgsql(builder.Configuration.GetConnectionString("Auth"))
        .UseSnakeCaseNamingConvention());

builder.Services.Configure<RefreshTokenOptions>(builder.Configuration.GetSection("RefreshToken"));
builder.Services.AddScoped<RefreshTokenService>();

builder.Services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.Configure<BootstrapAdminOptions>(builder.Configuration.GetSection("BootstrapAdmin"));

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));
var jwtOptions = builder.Configuration.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();

builder.Services.AddSingleton(services => JwtSigningKey.Load(
    Path.Combine(builder.Environment.ContentRootPath, jwtOptions.PrivateKeyPath),
    createIfMissing: builder.Environment.IsDevelopment(),
    services.GetRequiredService<ILoggerFactory>().CreateLogger<JwtSigningKey>()));

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<TokenService>();

// Не больше 10 попыток входа в минуту с одного адреса
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("login", http => RateLimitPartition.GetFixedWindowLimiter(
        http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(1),
        }));
});

var app = builder.Build();

app.Services.GetRequiredService<JwtSigningKey>();

await app.MigrateDatabaseAsync<AuthDbContext>();

using (var scope = app.Services.CreateScope())
{
    await AdminSeeder.SeedAsync(scope.ServiceProvider);
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<AuthDbContext>().Database.MigrateAsync();
}

app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapAuthEndpoints();
app.MapUserEndpoints();

app.Run();
