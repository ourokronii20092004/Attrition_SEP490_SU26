using BuildingBlocks.Authentication;
using BuildingBlocks.Caching;
using BuildingBlocks.Persistence;
using BuildingBlocks.Web;
using FluentValidation;
using FluentValidation.AspNetCore;
using Forum.Service.Data;
using Forum.Service.Repositories;
using Forum.Service.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<ForumDbContext>(opt =>
    opt.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        npgsql =>
        {
            npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "forum");
            // Survive transient Postgres blips (restart, brief network drop) by retrying instead
            // of surfacing an error to the user. Manual transactions are wrapped in an execution
            // strategy so they stay retry-safe (see ForumRepository).
            npgsql.EnableRetryOnFailure(maxRetryCount: 3, maxRetryDelay: TimeSpan.FromSeconds(2), errorCodesToAdd: null);
        }));

builder.Services.AddScoped<DbContext>(sp => sp.GetRequiredService<ForumDbContext>());
builder.Services.AddDbWarmup();
builder.Services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
builder.Services.AddScoped<IForumRepository, ForumRepository>();
builder.Services.AddScoped<IForumService, ForumService>();
builder.Services.AddAttritionCache(builder.Configuration, "forum");

// Internal client to dispatch reply/mention notifications to Identity (3s timeout, retry, internal key).
builder.Services.AddHttpClient<Forum.Service.Clients.NotificationClient>(c =>
{
    c.BaseAddress = new Uri(builder.Configuration["Services:Identity"]
        ?? throw new InvalidOperationException("Services:Identity not configured"));
    c.Timeout = TimeSpan.FromSeconds(3);
}).AddTransientRetry();

builder.Services.AddAttritionJwtAuth(builder.Configuration);

builder.Services.AddValidatorsFromAssemblyContaining<Program>();
builder.Services.AddFluentValidationAutoValidation();

builder.Services.AddAttritionControllers();
builder.Services.AddAttritionSwagger("Forum.Service");

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ForumDbContext>();
    await db.Database.MigrateAsync();
    await CategorySeeder.SeedCategoriesAsync(db,
        scope.ServiceProvider.GetRequiredService<ILogger<Program>>());
}

app.UseAttritionPipeline();
app.Run();
