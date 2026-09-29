using System.Text.Json.Serialization;
using Format.Layout;
using Format.Print.Api.Composition;
using Format.Print.Api.Cups;
using Format.Print.Api.Data;
using Format.Print.Api.Printing;
using Format.Print.Api.Storage;
using Format.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

builder.Services.AddFormatAuthentication(builder.Configuration);
builder.Services.AddHttpContextAccessor();

builder.Services.AddDbContext<PrintDbContext>(options =>
    options
        .UseNpgsql(builder.Configuration.GetConnectionString("Print"))
        .UseSnakeCaseNamingConvention());

builder.Services.Configure<SpoolOptions>(builder.Configuration.GetSection("Spool"));
builder.Services.AddSingleton<Spool>();
builder.Services.AddSingleton(TimeProvider.System);

builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// Раскладка
builder.Services.AddSingleton(builder.Configuration.GetSection("Plotter").Get<PlotterSettings>() ?? new PlotterSettings());
builder.Services.AddSingleton<LayoutPlanner>();
builder.Services.AddSingleton<SheetComposer>();
builder.Services.AddScoped<OrderProcessor>();
builder.Services.AddHostedService<PrintQueueWorker>();
builder.Services.AddScoped<JobTracker>();
builder.Services.AddHostedService<JobTrackingWorker>();

// Сервис хранения - от имени пользователя
builder.Services.Configure<StorageOptions>(builder.Configuration.GetSection("Storage"));
builder.Services.AddTransient<ForwardUserTokenHandler>();
builder.Services
    .AddHttpClient<StorageClient>((services, http) =>
    {
        var storage = services.GetRequiredService<IOptions<StorageOptions>>().Value;
        http.BaseAddress = new Uri(storage.BaseUrl);
    })
    .AddHttpMessageHandler<ForwardUserTokenHandler>();

builder.Services.Configure<CupsOptions>(builder.Configuration.GetSection("Cups"));

builder.Services.AddHttpClient<CupsClient>((services, http) =>
{
    var cups = services.GetRequiredService<IOptions<CupsOptions>>().Value;
    http.BaseAddress = new Uri(cups.BaseUrl);
    http.Timeout = TimeSpan.FromMinutes(5);
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { status = "ok" })).AllowAnonymous();
app.MapPrintEndpoints();

app.Run();