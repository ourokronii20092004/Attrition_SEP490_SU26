using BuildingBlocks.Authentication;
using BuildingBlocks.Web;
using FluentValidation;
using FluentValidation.AspNetCore;
using Identity.Service.Data;
using Identity.Service.Repositories;
using Identity.Service.Services;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);

// Fail fast on a missing/garbage JWT secret rather than throwing an opaque 500 on the first login.
var jwtSecret = builder.Configuration["Jwt:Secret"];
if (string.IsNullOrWhiteSpace(jwtSecret))
    throw new InvalidOperationException("FATAL: 'Jwt:Secret' is not configured.");
foreach (var key in new[] { "Jwt:AccessTokenExpiryMinutes", "Jwt:RefreshTokenExpiryDays" })
{
    var raw = builder.Configuration[key];
    if (raw is not null && !double.TryParse(raw, out _))
        throw new InvalidOperationException($"FATAL: '{key}' value '{raw}' is not a valid number.");
}

builder.Services.Configure<FormOptions>(o => o.MultipartBodyLengthLimit = 20 * 1024 * 1024);

builder.Services.AddDbContext<IdentityDbContext>(opt =>
    opt.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        npgsql =>
        {
            npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "identity");
            // Survive transient Postgres blips by retrying instead of erroring the user.
            npgsql.EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(5), errorCodesToAdd: null);
        }));

builder.Services.AddScoped<DbContext>(sp => sp.GetRequiredService<IdentityDbContext>());
builder.Services.AddScoped<IUserRepository, UserRepository>();

builder.Services.AddSingleton<TokenService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IAccountService, AccountService>();
builder.Services.AddScoped<IAdminUserService, AdminUserService>();
builder.Services.AddSingleton<IFileStorage, LocalFileStorage>();
builder.Services.AddScoped<IFileService, FileService>();
// Use real SMTP when configured (Smtp:Host/Username/Password); otherwise log to console in dev.
if (SmtpEmailService.IsConfigured(builder.Configuration))
    builder.Services.AddSingleton<IEmailService, SmtpEmailService>();
else
    builder.Services.AddSingleton<IEmailService, ConsoleEmailService>();

builder.Services.AddAttritionJwtAuth(builder.Configuration);

builder.Services.AddValidatorsFromAssemblyContaining<Program>();
builder.Services.AddFluentValidationAutoValidation();

builder.Services.AddAttritionControllers();
builder.Services.AddAttritionSwagger("Identity.Service");

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
    await db.Database.MigrateAsync();
    await AdminSeeder.SeedAdminAsync(db, app.Configuration,
        scope.ServiceProvider.GetRequiredService<ILogger<Program>>());
}

// Serve uploaded avatars/backgrounds under the public prefix the storage layer returns.
var uploadPath = builder.Configuration["FileUpload:UploadPath"] ?? "/app/uploads";
Directory.CreateDirectory(uploadPath);
var mediaPrefix = builder.Configuration["FileUpload:PublicPrefix"] ?? "/api/account/media";
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(Path.GetFullPath(uploadPath)),
    RequestPath = mediaPrefix,
    OnPrepareResponse = BuildingBlocks.Web.MediaSecurityHeaders.OnPrepareStaticResponse
});

app.UseAttritionPipeline();
app.Run();
