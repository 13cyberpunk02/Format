using Format.Print.Api.Composition;
using Format.Print.Api.Cups;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

builder.Services.Configure<CupsOptions>(builder.Configuration.GetSection("Cups"));

builder.Services.AddHttpClient<CupsClient>((services, http) =>
{
    var cups = services.GetRequiredService<IOptions<CupsOptions>>().Value;
    http.BaseAddress = new Uri(cups.BaseUrl);
    http.Timeout = TimeSpan.FromMinutes(5);
});

builder.Services.AddSingleton<SheetComposer>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.Run();