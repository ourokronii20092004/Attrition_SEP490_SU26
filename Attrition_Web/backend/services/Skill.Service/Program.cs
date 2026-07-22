using BuildingBlocks.Authentication;
using BuildingBlocks.Caching;
using BuildingBlocks.Persistence;
using BuildingBlocks.Web;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.EntityFrameworkCore;
using Skill.Service.Data;
using Skill.Service.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDbContext<SkillDbContext>(opt => opt.UseNpgsql(
    builder.Configuration.GetConnectionString("DefaultConnection"), npgsql =>
    {
        npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "skill");
        npgsql.EnableRetryOnFailure(3, TimeSpan.FromSeconds(2), null);
    }));
builder.Services.AddScoped<DbContext>(sp => sp.GetRequiredService<SkillDbContext>());
builder.Services.AddDbWarmup();
builder.Services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
builder.Services.AddScoped<ISkillRepository, Skill.Service.Repositories.SkillRepository>();
builder.Services.AddScoped<ISkillService, Skill.Service.Services.SkillService>();
builder.Services.AddAttritionCache(builder.Configuration, "skill");
builder.Services.AddAttritionJwtAuth(builder.Configuration);
builder.Services.AddValidatorsFromAssemblyContaining<Program>();
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddAttritionControllers();
builder.Services.AddAttritionSwagger("Skill.Service");
var app = builder.Build();
using (var scope = app.Services.CreateScope())
    await scope.ServiceProvider.GetRequiredService<SkillDbContext>().Database.MigrateAsync();
app.UseAttritionPipeline();
app.Run();
