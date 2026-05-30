using BuildingBlocks.Authentication;
using BuildingBlocks.Caching;
using BuildingBlocks.Persistence;
using BuildingBlocks.Web;
using Enemy.Service.Data;
using Enemy.Service.Repositories;
using Enemy.Service.Services;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<EnemyDbContext>(opt =>
    opt.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "enemy")));

builder.Services.AddScoped<DbContext>(sp => sp.GetRequiredService<EnemyDbContext>());
builder.Services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
builder.Services.AddScoped<IEnemyRepository, EnemyRepository>();
builder.Services.AddScoped<IEnemyService, EnemyService>();
builder.Services.AddAttritionCache(builder.Configuration, "enemy");

builder.Services.AddAttritionJwtAuth(builder.Configuration);

builder.Services.AddValidatorsFromAssemblyContaining<Program>();
builder.Services.AddFluentValidationAutoValidation();

builder.Services.AddAttritionControllers();
builder.Services.AddAttritionSwagger("Enemy.Service");

var app = builder.Build();

using (var scope = app.Services.CreateScope())
    await scope.ServiceProvider.GetRequiredService<EnemyDbContext>().Database.MigrateAsync();

app.UseAttritionPipeline();
app.Run();
