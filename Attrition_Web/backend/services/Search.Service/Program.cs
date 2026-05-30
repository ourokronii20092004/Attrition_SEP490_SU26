using BuildingBlocks.Authentication;
using BuildingBlocks.Web;
using Search.Service.Clients;
using Search.Service.Services;

var builder = WebApplication.CreateBuilder(args);

// Typed clients to downstream internal endpoints, each with a hard 3s timeout.
// Failure of any one degrades to partial results (see SearchService.Safe).
void AddInternalClient<TClient>(string serviceKey) where TClient : class
{
    builder.Services.AddHttpClient<TClient>(c =>
    {
        c.BaseAddress = new Uri(builder.Configuration[$"Services:{serviceKey}"]
            ?? throw new InvalidOperationException($"Services:{serviceKey} not configured"));
        c.Timeout = TimeSpan.FromSeconds(3);
    }).AddTransientRetry();
}

AddInternalClient<WikiSearchClient>("Wiki");
AddInternalClient<ForumSearchClient>("Forum");
AddInternalClient<IdentitySearchClient>("Identity");
AddInternalClient<EnemySearchClient>("Enemy");

builder.Services.AddScoped<ISearchService, SearchService>();

builder.Services.AddAttritionJwtAuth(builder.Configuration);

builder.Services.AddControllers();
builder.Services.AddAttritionSwagger("Search.Service");

var app = builder.Build();
app.UseAttritionPipeline();
app.Run();
