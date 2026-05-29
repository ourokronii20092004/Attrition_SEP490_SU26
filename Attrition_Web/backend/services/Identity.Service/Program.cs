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

builder.Services.Configure<FormOptions>(o => o.MultipartBodyLengthLimit = 20 * 1024 * 1024);

builder.Services.AddDbContext<IdentityDbContext>(opt =>
    opt.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "identity")));

builder.Services.AddScoped<DbContext>(sp => sp.GetRequiredService<IdentityDbContext>());
builder.Services.AddScoped<IUserRepository, UserRepository>();

builder.Services.AddSingleton<TokenService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IAccountService, AccountService>();
builder.Services.AddScoped<IAdminUserService, AdminUserService>();
builder.Services.AddSingleton<IFileStorage, LocalFileStorage>();
builder.Services.AddScoped<IFileService, FileService>();
builder.Services.AddSingleton<IEmailService, ConsoleEmailService>();

builder.Services.AddAttritionJwtAuth(builder.Configuration);

builder.Services.AddValidatorsFromAssemblyContaining<Program>();
builder.Services.AddFluentValidationAutoValidation();

builder.Services.AddControllers();
builder.Services.AddAttritionSwagger("Identity.Service");

var app = builder.Build();

using (var scope = app.Services.CreateScope())
    await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().Database.MigrateAsync();

// Serve uploaded avatars/backgrounds under the public prefix the storage layer returns.
var uploadPath = builder.Configuration["FileUpload:UploadPath"] ?? "/app/uploads";
Directory.CreateDirectory(uploadPath);
var mediaPrefix = builder.Configuration["FileUpload:PublicPrefix"] ?? "/api/account/media";
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(Path.GetFullPath(uploadPath)),
    RequestPath = mediaPrefix
});

app.UseAttritionPipeline();
app.Run();
