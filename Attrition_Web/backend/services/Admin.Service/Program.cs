using Admin.Service.Clients;
using Admin.Service.Services;
using BuildingBlocks.Authentication;
using BuildingBlocks.Web;

var builder = WebApplication.CreateBuilder(args);

// Typed stats clients to each owning service's internal endpoint (3s timeout, degrade per-source).
void AddStatsClient<TClient>(string serviceKey) where TClient : class
{
    builder.Services.AddHttpClient<TClient>(c =>
    {
        c.BaseAddress = new Uri(builder.Configuration[$"Services:{serviceKey}"]
            ?? throw new InvalidOperationException($"Services:{serviceKey} not configured"));
        c.Timeout = TimeSpan.FromSeconds(3);
    });
}

AddStatsClient<IdentityStatsClient>("Identity");
AddStatsClient<WikiStatsClient>("Wiki");
AddStatsClient<ForumStatsClient>("Forum");
AddStatsClient<EnemyStatsClient>("Enemy");
AddStatsClient<AssetsStatsClient>("Assets");
AddStatsClient<MusicStatsClient>("Music");

builder.Services.AddScoped<IAdminStatsService, AdminStatsService>();

builder.Services.AddAttritionJwtAuth(builder.Configuration);

builder.Services.AddControllers();
builder.Services.AddAttritionSwagger("Admin.Service");

var app = builder.Build();
app.UseAttritionPipeline();
app.Run();
