using BuildingBlocks.Authentication;
using BuildingBlocks.Persistence;
using BuildingBlocks.Web;
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
        npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "character")));

builder.Services.AddScoped<DbContext>(sp => sp.GetRequiredService<CharacterDbContext>());
builder.Services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
builder.Services.AddScoped<ICharacterRepository, CharacterRepository>();
builder.Services.AddScoped<ICharacterService, CharacterService>();

builder.Services.AddAttritionJwtAuth(builder.Configuration);

builder.Services.AddValidatorsFromAssemblyContaining<Program>();
builder.Services.AddFluentValidationAutoValidation();

builder.Services.AddControllers();
builder.Services.AddAttritionSwagger("Character.Service");

var app = builder.Build();

using (var scope = app.Services.CreateScope())
    await scope.ServiceProvider.GetRequiredService<CharacterDbContext>().Database.MigrateAsync();

app.UseAttritionPipeline();
app.Run();
