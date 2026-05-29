using BuildingBlocks.Authentication;
using BuildingBlocks.Persistence;
using BuildingBlocks.Web;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.EntityFrameworkCore;
using Wiki.Service.Data;
using Wiki.Service.Repositories;
using Wiki.Service.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<WikiDbContext>(opt =>
    opt.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "wiki")));

builder.Services.AddScoped<DbContext>(sp => sp.GetRequiredService<WikiDbContext>());
builder.Services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
builder.Services.AddScoped<IWikiRepository, WikiRepository>();
builder.Services.AddScoped<IWikiService, WikiService>();

builder.Services.AddAttritionJwtAuth(builder.Configuration);

builder.Services.AddValidatorsFromAssemblyContaining<Program>();
builder.Services.AddFluentValidationAutoValidation();

builder.Services.AddControllers();
builder.Services.AddAttritionSwagger("Wiki.Service");

var app = builder.Build();

using (var scope = app.Services.CreateScope())
    await scope.ServiceProvider.GetRequiredService<WikiDbContext>().Database.MigrateAsync();

app.UseAttritionPipeline();
app.Run();
