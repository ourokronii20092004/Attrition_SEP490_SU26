using BuildingBlocks.Authentication;
using BuildingBlocks.Caching;
using BuildingBlocks.Persistence;
using BuildingBlocks.Web;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Music.Service.Data;
using Music.Service.Services;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = 100 * 1024 * 1024);
builder.Services.Configure<FormOptions>(o => o.MultipartBodyLengthLimit = 100 * 1024 * 1024);

builder.Services.AddDbContext<MusicDbContext>(opt =>
    opt.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "music")));

builder.Services.AddScoped<DbContext>(sp => sp.GetRequiredService<MusicDbContext>());
builder.Services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
builder.Services.AddScoped<IAlbumService, AlbumService>();
builder.Services.AddScoped<ITrackService, TrackService>();
builder.Services.AddScoped<IFavoriteService, FavoriteService>();
builder.Services.AddScoped<IPlaylistService, PlaylistService>();
builder.Services.AddHostedService<TempFileCleanupService>();
builder.Services.AddAttritionCache(builder.Configuration, "music");

builder.Services.AddAttritionJwtAuth(builder.Configuration);

builder.Services.AddValidatorsFromAssemblyContaining<Program>();
builder.Services.AddFluentValidationAutoValidation();

builder.Services.AddAttritionControllers();
builder.Services.AddAttritionSwagger("Music.Service");

var app = builder.Build();

using (var scope = app.Services.CreateScope())
    await scope.ServiceProvider.GetRequiredService<MusicDbContext>().Database.MigrateAsync();

// Serve cover art (audio is streamed via the controller's PhysicalFile range endpoint).
var uploadPath = builder.Configuration["FileUpload:UploadPath"] ?? "/app/uploads";
Directory.CreateDirectory(uploadPath);
var mediaPrefix = builder.Configuration["FileUpload:PublicPrefix"] ?? "/api/music/media";
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(Path.GetFullPath(uploadPath)),
    RequestPath = mediaPrefix,
    OnPrepareResponse = ctx =>
    {
        // Never serve the in-progress upload staging dir — a guessed temp filename could otherwise
        // be downloaded before the cleanup sweep runs.
        var path = ctx.File.PhysicalPath;
        if (path != null && path.Contains(Path.Combine("music", "temp"), StringComparison.OrdinalIgnoreCase))
        {
            ctx.Context.Response.StatusCode = StatusCodes.Status404NotFound;
            ctx.Context.Response.ContentLength = 0;
            ctx.Context.Response.Body = Stream.Null;
            return;
        }
        ctx.Context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    }
});

app.UseAttritionPipeline();
app.Run();
