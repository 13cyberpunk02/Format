using Amazon.Runtime;
using Amazon.S3;
using Format.Layout;
using Format.Security;
using Format.ServiceDefaults;
using Format.Storage.Api.Data;
using Format.Storage.Api.Endpoints;
using Format.Storage.Api.Files;
using Format.Storage.Api.Pdf;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

const long maxUploadBytes = 200L * 1024 * 1024;

var builder = WebApplication.CreateBuilder(args);
builder.AddFormatDefaults();

builder.WebHost.ConfigureKestrel(k => k.Limits.MaxRequestBodySize = maxUploadBytes);
builder.Services.Configure<FormOptions>(o => o.MultipartBodyLengthLimit = maxUploadBytes);


builder.Services.AddOpenApi();

builder.Services.AddFormatAuthentication(builder.Configuration);
builder.Services.AddDbContext<StorageDbContext>(options =>
    options
        .UseNpgsql(builder.Configuration.GetConnectionString("Storage"))
        .UseSnakeCaseNamingConvention());

builder.Services.AddSingleton(builder.Configuration.GetSection("Plotter").Get<PlotterSettings>() ?? new PlotterSettings());
builder.Services.AddSingleton<FormatValidator>();
builder.Services.AddSingleton<PdfPageSplitter>();

builder.Services.Configure<S3Options>(builder.Configuration.GetSection("S3"));
builder.Services.AddSingleton<IAmazonS3>(services =>
{
    var s3 = services.GetRequiredService<IOptions<S3Options>>().Value;

    var config = new AmazonS3Config
    {
        ServiceURL = s3.ServiceUrl,
        ForcePathStyle = true,
        AuthenticationRegion = "us-east-1",
        RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
        ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED,
    };

    return new AmazonS3Client(new BasicAWSCredentials(s3.AccessKey, s3.SecretKey), config);
});
builder.Services.AddSingleton<IDrawingFileStore, S3DrawingFileStore>();

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapDrawingEndpoints();

await app.MigrateDatabaseAsync<StorageDbContext>();
app.Run();