using Assets.Service.Data;
using Assets.Service.Repositories;
using Assets.Service.Services;
using BuildingBlocks.Authentication;
using BuildingBlocks.Persistence;
using BuildingBlocks.Web;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);

// Allow uploads up to the configured asset cap (default 50MB) plus headroom for multipart overhead.
var maxAssetMb = long.TryParse(builder.Configuration["FileUpload:MaxImageSizeMB"], out var mb) && mb > 0 ? mb : 50;
var maxBytes = (maxAssetMb + 5) * 1024 * 1024;
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = maxBytes);
builder.Services.Configure<FormOptions>(o => o.MultipartBodyLengthLimit = maxBytes);

builder.Services.AddDbContext<AssetsDbContext>(opt =>
    opt.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        npgsql =>
        {
            npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "assets");
            // Survive transient Postgres blips by retrying instead of erroring the user.
            npgsql.EnableRetryOnFailure(maxRetryCount: 3, maxRetryDelay: TimeSpan.FromSeconds(2), errorCodesToAdd: null);
        }));

builder.Services.AddScoped<DbContext>(sp => sp.GetRequiredService<AssetsDbContext>());
builder.Services.AddDbWarmup();
builder.Services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
builder.Services.AddScoped<IAssetRepository, AssetRepository>();
builder.Services.AddSingleton<IFileStorage, LocalFileStorage>();
builder.Services.AddScoped<IAssetService, AssetService>();

builder.Services.AddAttritionJwtAuth(builder.Configuration);

builder.Services.AddValidatorsFromAssemblyContaining<Program>();
builder.Services.AddFluentValidationAutoValidation();

builder.Services.AddAttritionControllers();
builder.Services.AddAttritionSwagger("Assets.Service");

var app = builder.Build();

using (var scope = app.Services.CreateScope())
    await scope.ServiceProvider.GetRequiredService<AssetsDbContext>().Database.MigrateAsync();

var uploadPath = builder.Configuration["FileUpload:UploadPath"] ?? "/app/uploads";
Directory.CreateDirectory(uploadPath);
var mediaPrefix = builder.Configuration["FileUpload:PublicPrefix"] ?? "/api/assets/media";
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(Path.GetFullPath(uploadPath)),
    RequestPath = mediaPrefix,
    OnPrepareResponse = BuildingBlocks.Web.MediaSecurityHeaders.OnPrepareStaticResponse
});

app.UseAttritionPipeline();
app.Run();
