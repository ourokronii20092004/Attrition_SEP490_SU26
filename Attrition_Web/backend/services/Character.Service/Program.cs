using BuildingBlocks.Authentication;
using BuildingBlocks.Persistence;
using BuildingBlocks.Web;
using Character.Service.Clients;
using Character.Service.Data;
using Character.Service.Repositories;
using Character.Service.Services;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<CharacterDbContext>(opt =>
    opt.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        npgsql =>
        {
            npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "character");
            // Survive transient Postgres blips by retrying instead of erroring the game client/user.
            npgsql.EnableRetryOnFailure(maxRetryCount: 3, maxRetryDelay: TimeSpan.FromSeconds(2), errorCodesToAdd: null);
        }));

builder.Services.AddScoped<DbContext>(sp => sp.GetRequiredService<CharacterDbContext>());
builder.Services.AddDbWarmup();
builder.Services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
builder.Services.AddScoped<ICharacterRepository, CharacterRepository>();
builder.Services.AddScoped<ICharacterService, CharacterService>();

// Internal client to resolve owner usernames from Identity (3s timeout, GET/POST retry, internal key).
builder.Services.AddHttpClient<IdentityClient>(c =>
{
    c.BaseAddress = new Uri(builder.Configuration["Services:Identity"]
        ?? throw new InvalidOperationException("Services:Identity not configured"));
    c.Timeout = TimeSpan.FromSeconds(3);
}).AddTransientRetry();

builder.Services.AddAttritionJwtAuth(builder.Configuration);

builder.Services.AddValidatorsFromAssemblyContaining<Program>();
builder.Services.AddFluentValidationAutoValidation();

builder.Services.AddAttritionControllers();
builder.Services.AddAttritionSwagger("Character.Service");

var app = builder.Build();

using (var scope = app.Services.CreateScope())
    await scope.ServiceProvider.GetRequiredService<CharacterDbContext>().Database.MigrateAsync();

app.UseAttritionPipeline();
app.Run();
