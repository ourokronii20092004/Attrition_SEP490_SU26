# 03 — Backend API

Full ASP.NET Core Web API implementation. Covers `Program.cs`, all controllers, services, DTOs, middleware, and validators.

---

## `Program.cs`

```csharp
using System.Text;
using Attrition.API.Data;
using Attrition.API.Middleware;
using Attrition.API.Services;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

// Database
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Redis
builder.Services.AddSingleton<IConnectionMultiplexer>(
    ConnectionMultiplexer.Connect(builder.Configuration["Redis:ConnectionString"] ?? "localhost:6379"));
builder.Services.AddScoped<CacheService>();

// JWT Authentication
var jwtSecret = builder.Configuration["Jwt:Secret"] ?? throw new Exception("JWT Secret not configured");
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ClockSkew = TimeSpan.Zero
        };
    });
builder.Services.AddAuthorization();

// Services
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<WikiService>();
builder.Services.AddScoped<ForumService>();
builder.Services.AddScoped<FileService>();

// FluentValidation
builder.Services.AddValidatorsFromAssemblyContaining<Program>();
builder.Services.AddFluentValidationAutoValidation();

// CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.WithOrigins("http://localhost:3000", "http://web:3000")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials());
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Migrate + Seed
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
    await SeedData.Initialize(db);
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseMiddleware<ErrorHandlingMiddleware>();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.UseStaticFiles();        // For serving uploaded avatars
app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

app.Run();
```

---

## DTOs

All DTOs go in `api/DTOs/`. Use records for immutability.

### `ApiResponse.cs`
```csharp
namespace Attrition.API.DTOs;

public record ApiResponse<T>(bool Success, T? Data = default, string? Error = null);
public record ApiResponse(bool Success, string? Error = null);
```

### `AuthDTOs.cs`
```csharp
namespace Attrition.API.DTOs;

public record RegisterRequest(string Username, string Password);
public record LoginRequest(string Username, string Password);
public record AuthResponse(string AccessToken, string RefreshToken, UserDto User);
public record RefreshRequest(string RefreshToken);
public record ChangePasswordRequest(string CurrentPassword, string NewPassword);
```

### `UserDTOs.cs`
```csharp
namespace Attrition.API.DTOs;

public record UserDto(Guid Id, string Username, string Role, string? AvatarUrl,
    string? Bio, DateTime JoinedAt, int PostCount, int ContributionCount, bool MustChangePassword);
public record UpdateProfileRequest(string? Bio);
public record UserListItem(Guid Id, string Username, string Role, bool IsBanned, DateTime JoinedAt);
```

### `WikiDTOs.cs`
```csharp
namespace Attrition.API.DTOs;

public record WikiCategoryDto(int Id, string Name, string Slug, string Description, string? IconUrl, int ArticleCount);
public record WikiArticleListDto(Guid Id, string Title, string Slug, string CategorySlug, string? AuthorName, DateTime UpdatedAt);
public record WikiArticleDto(Guid Id, string Title, string Slug, string CategorySlug, string Content,
    string? AuthorName, string? LastEditorName, string Status, DateTime CreatedAt, DateTime UpdatedAt);
public record CreateArticleRequest(string Title, int CategoryId, string Content, string Status);
public record UpdateArticleRequest(string? Title, string? Content, string? Status, string? ChangeNote);
public record SuggestEditRequest(string SuggestedContent, string? ChangeNote);
public record WikiContributionDto(Guid Id, Guid ArticleId, string ArticleTitle, string ContributorName,
    string SuggestedContent, string? ChangeNote, string Status, DateTime SubmittedAt);
public record ReviewContributionRequest(string Status); // "Approved" or "Rejected"
```

### `ForumDTOs.cs`
```csharp
namespace Attrition.API.DTOs;

public record ForumCategoryDto(int Id, string Name, string Slug, string Description, int ThreadCount, DateTime? LatestActivity);
public record ForumThreadListDto(Guid Id, string Title, string AuthorName, string? AuthorAvatar,
    bool IsPinned, bool IsLocked, int ReplyCount, DateTime CreatedAt, DateTime LastReplyAt);
public record ForumThreadDto(Guid Id, string Title, string CategorySlug, string AuthorName,
    bool IsPinned, bool IsLocked, int ReplyCount, DateTime CreatedAt);
public record CreateThreadRequest(int CategoryId, string Title, string Content); // Content = first post
public record ForumPostDto(Guid Id, Guid ThreadId, string AuthorName, string? AuthorAvatar,
    string AuthorRole, string Content, DateTime CreatedAt, DateTime? UpdatedAt,
    int LikeCount, int DislikeCount, string? CurrentUserReaction);
public record CreatePostRequest(string Content);
public record UpdatePostRequest(string Content);
public record ReactRequest(string ReactionType); // "like" or "dislike"
public record PaginatedResponse<T>(List<T> Items, int TotalCount, int Page, int PageSize);
```

---

## Controllers

### `AuthController.cs`
```csharp
using Attrition.API.DTOs;
using Attrition.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Attrition.API.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly AuthService _auth;
    public AuthController(AuthService auth) => _auth = auth;

    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterRequest request)
    {
        var result = await _auth.RegisterAsync(request);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        var result = await _auth.LoginAsync(request);
        return result.Success ? Ok(result) : Unauthorized(result);
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh(RefreshRequest request)
    {
        var result = await _auth.RefreshAsync(request);
        return result.Success ? Ok(result) : Unauthorized(result);
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        var userId = Guid.Parse(User.FindFirst("sub")!.Value);
        var result = await _auth.GetCurrentUserAsync(userId);
        return result.Success ? Ok(result) : NotFound(result);
    }

    [Authorize]
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request)
    {
        var userId = Guid.Parse(User.FindFirst("sub")!.Value);
        var result = await _auth.ChangePasswordAsync(userId, request);
        return result.Success ? Ok(result) : BadRequest(result);
    }
}
```

### `UsersController.cs`
```csharp
using Attrition.API.DTOs;
using Attrition.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Attrition.API.Controllers;

[ApiController]
[Route("api/users")]
public class UsersController : ControllerBase
{
    private readonly AuthService _auth;
    private readonly FileService _files;
    public UsersController(AuthService auth, FileService files) { _auth = auth; _files = files; }

    [HttpGet("{username}/profile")]
    public async Task<IActionResult> GetProfile(string username)
    {
        var result = await _auth.GetProfileByUsernameAsync(username);
        return result.Success ? Ok(result) : NotFound(result);
    }

    [Authorize]
    [HttpPut("profile")]
    public async Task<IActionResult> UpdateProfile(UpdateProfileRequest request)
    {
        var userId = Guid.Parse(User.FindFirst("sub")!.Value);
        var result = await _auth.UpdateProfileAsync(userId, request);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [Authorize]
    [HttpPost("avatar")]
    public async Task<IActionResult> UploadAvatar(IFormFile file)
    {
        var userId = Guid.Parse(User.FindFirst("sub")!.Value);
        var result = await _files.UploadAvatarAsync(userId, file);
        return result.Success ? Ok(result) : BadRequest(result);
    }
}
```

### `AdminController.cs`
```csharp
using Attrition.API.DTOs;
using Attrition.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Attrition.API.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Roles = "Admin")]
public class AdminController : ControllerBase
{
    private readonly AuthService _auth;
    public AdminController(AuthService auth) => _auth = auth;

    [HttpGet("users")]
    public async Task<IActionResult> ListUsers([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var result = await _auth.ListUsersAsync(page, pageSize);
        return Ok(result);
    }

    [HttpPut("users/{id}/role")]
    public async Task<IActionResult> ChangeRole(Guid id, [FromBody] string role)
    {
        var result = await _auth.ChangeRoleAsync(id, role);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("users/{id}/ban")]
    public async Task<IActionResult> ToggleBan(Guid id)
    {
        var result = await _auth.ToggleBanAsync(id);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPut("users/{id}/reset-password")]
    public async Task<IActionResult> ResetPassword(Guid id, [FromBody] string newPassword)
    {
        var result = await _auth.AdminResetPasswordAsync(id, newPassword);
        return result.Success ? Ok(result) : BadRequest(result);
    }
}
```

### `WikiController.cs`
```csharp
using Attrition.API.DTOs;
using Attrition.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Attrition.API.Controllers;

[ApiController]
[Route("api/wiki")]
public class WikiController : ControllerBase
{
    private readonly WikiService _wiki;
    public WikiController(WikiService wiki) => _wiki = wiki;

    [HttpGet("categories")]
    public async Task<IActionResult> GetCategories() => Ok(await _wiki.GetCategoriesAsync());

    [HttpGet("articles")]
    public async Task<IActionResult> GetArticles([FromQuery] string? category, [FromQuery] string? search,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        => Ok(await _wiki.GetArticlesAsync(category, search, page, pageSize));

    [HttpGet("articles/{slug}")]
    public async Task<IActionResult> GetArticle(string slug)
    {
        var result = await _wiki.GetArticleBySlugAsync(slug);
        return result != null ? Ok(new ApiResponse<WikiArticleDto>(true, result)) : NotFound(new ApiResponse(false, "Article not found"));
    }

    [HttpGet("articles/{id:guid}/revisions")]
    public async Task<IActionResult> GetRevisions(Guid id) => Ok(await _wiki.GetRevisionsAsync(id));

    // Admin-only: create article
    [Authorize(Roles = "Admin")]
    [HttpPost("articles")]
    public async Task<IActionResult> CreateArticle(CreateArticleRequest request)
    {
        var userId = Guid.Parse(User.FindFirst("sub")!.Value);
        var result = await _wiki.CreateArticleAsync(request, userId);
        return result.Success ? CreatedAtAction(nameof(GetArticle), new { slug = result.Data }, result) : BadRequest(result);
    }

    // Admin-only: update article
    [Authorize(Roles = "Admin")]
    [HttpPut("articles/{id:guid}")]
    public async Task<IActionResult> UpdateArticle(Guid id, UpdateArticleRequest request)
    {
        var userId = Guid.Parse(User.FindFirst("sub")!.Value);
        var result = await _wiki.UpdateArticleAsync(id, request, userId);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    // User: suggest an edit
    [Authorize]
    [HttpPost("articles/{id:guid}/suggest")]
    public async Task<IActionResult> SuggestEdit(Guid id, SuggestEditRequest request)
    {
        var userId = Guid.Parse(User.FindFirst("sub")!.Value);
        var result = await _wiki.SubmitContributionAsync(id, request, userId);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    // Admin: list pending contributions
    [Authorize(Roles = "Admin")]
    [HttpGet("contributions")]
    public async Task<IActionResult> GetContributions([FromQuery] string status = "Pending")
        => Ok(await _wiki.GetContributionsAsync(status));

    // Admin: review contribution
    [Authorize(Roles = "Admin")]
    [HttpPost("contributions/{id:guid}/review")]
    public async Task<IActionResult> ReviewContribution(Guid id, ReviewContributionRequest request)
    {
        var userId = Guid.Parse(User.FindFirst("sub")!.Value);
        var result = await _wiki.ReviewContributionAsync(id, request, userId);
        return result.Success ? Ok(result) : BadRequest(result);
    }
}
```

### `ForumController.cs`
```csharp
using Attrition.API.DTOs;
using Attrition.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Attrition.API.Controllers;

[ApiController]
[Route("api/forum")]
public class ForumController : ControllerBase
{
    private readonly ForumService _forum;
    public ForumController(ForumService forum) => _forum = forum;

    [HttpGet("categories")]
    public async Task<IActionResult> GetCategories() => Ok(await _forum.GetCategoriesAsync());

    [HttpGet("threads")]
    public async Task<IActionResult> GetThreads([FromQuery] string? category, [FromQuery] string? search,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        => Ok(await _forum.GetThreadsAsync(category, search, page, pageSize));

    [HttpGet("threads/{id:guid}")]
    public async Task<IActionResult> GetThread(Guid id)
    {
        var result = await _forum.GetThreadAsync(id);
        return result != null ? Ok(new ApiResponse<ForumThreadDto>(true, result)) : NotFound(new ApiResponse(false, "Thread not found"));
    }

    [HttpGet("threads/{id:guid}/posts")]
    public async Task<IActionResult> GetPosts(Guid id, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        Guid? userId = User.Identity?.IsAuthenticated == true ? Guid.Parse(User.FindFirst("sub")!.Value) : null;
        return Ok(await _forum.GetPostsAsync(id, page, pageSize, userId));
    }

    [Authorize]
    [HttpPost("threads")]
    public async Task<IActionResult> CreateThread(CreateThreadRequest request)
    {
        var userId = Guid.Parse(User.FindFirst("sub")!.Value);
        var result = await _forum.CreateThreadAsync(request, userId);
        return result.Success ? CreatedAtAction(nameof(GetThread), new { id = result.Data }, result) : BadRequest(result);
    }

    [Authorize]
    [HttpPost("threads/{id:guid}/posts")]
    public async Task<IActionResult> CreatePost(Guid id, CreatePostRequest request)
    {
        var userId = Guid.Parse(User.FindFirst("sub")!.Value);
        var result = await _forum.CreatePostAsync(id, request, userId);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [Authorize]
    [HttpPut("posts/{id:guid}")]
    public async Task<IActionResult> UpdatePost(Guid id, UpdatePostRequest request)
    {
        var userId = Guid.Parse(User.FindFirst("sub")!.Value);
        var result = await _forum.UpdatePostAsync(id, request, userId);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [Authorize]
    [HttpDelete("posts/{id:guid}")]
    public async Task<IActionResult> DeletePost(Guid id)
    {
        var userId = Guid.Parse(User.FindFirst("sub")!.Value);
        var role = User.FindFirst("role")?.Value ?? "User";
        var result = await _forum.DeletePostAsync(id, userId, role);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [Authorize]
    [HttpPost("posts/{id:guid}/react")]
    public async Task<IActionResult> React(Guid id, ReactRequest request)
    {
        var userId = Guid.Parse(User.FindFirst("sub")!.Value);
        var result = await _forum.ToggleReactionAsync(id, userId, request);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    // Moderation (Admin only)
    [Authorize(Roles = "Admin")]
    [HttpPut("threads/{id:guid}/pin")]
    public async Task<IActionResult> TogglePin(Guid id) => Ok(await _forum.TogglePinAsync(id));

    [Authorize(Roles = "Admin")]
    [HttpPut("threads/{id:guid}/lock")]
    public async Task<IActionResult> ToggleLock(Guid id) => Ok(await _forum.ToggleLockAsync(id));

    [Authorize(Roles = "Admin")]
    [HttpDelete("threads/{id:guid}")]
    public async Task<IActionResult> DeleteThread(Guid id) => Ok(await _forum.DeleteThreadAsync(id));
}
```

---

## Services

Implement the following services in `api/Services/`. Each service receives `AppDbContext` and optionally `CacheService` via DI.

### `AuthService.cs`
Key responsibilities:
- **Register**: Validate password strength (via FluentValidation), check username uniqueness, hash with BCrypt, create user, return JWT tokens
- **Login**: Find user by username, verify BCrypt hash, check not banned, generate access + refresh tokens
- **Refresh**: Validate refresh token from DB, issue new access + refresh pair
- **JWT generation**: Claims include `sub` (user ID), `username`, `role`. Access token expires per config (default 15 min). Refresh token stored in User record with expiry.
- **GetCurrentUser / GetProfile**: Return `UserDto`
- **UpdateProfile**: Update bio
- **ChangePassword**: Verify old password, validate new, update hash, clear `MustChangePassword`
- **Admin functions**: ListUsers (paginated), ChangeRole, ToggleBan, AdminResetPassword

### `WikiService.cs`
Key responsibilities:
- **GetCategories**: Return categories with article counts. Cache in Redis.
- **GetArticles**: Filter by category slug, search by title, paginated
- **GetArticleBySlug**: Return full article. Cache in Redis.
- **CreateArticle**: Admin only. Auto-generate slug from title. Create initial revision.
- **UpdateArticle**: Admin only. Save old content as revision, update article. Invalidate cache.
- **SubmitContribution**: User suggests edit content + note. Creates `WikiContribution` with status "Pending". Increments user's contribution count.
- **GetContributions**: List by status (for admin review panel)
- **ReviewContribution**: Admin approves (applies content to article, creates revision) or rejects. Updates status and reviewer info.

### `ForumService.cs`
Key responsibilities:
- **GetCategories**: Return categories with thread counts and latest activity timestamp
- **GetThreads**: Filter by category, search by title, paginated. Pinned threads always first.
- **GetThread**: Return thread details
- **GetPosts**: Return posts for a thread, paginated, with author info, reaction counts, and current user's reaction if authenticated
- **CreateThread**: Create thread + first post. Increment user's post count.
- **CreatePost**: Add post to thread if not locked. Update thread's `LastReplyAt` and `ReplyCount`. Increment user's post count.
- **UpdatePost**: Only by the post author
- **DeletePost**: By author or admin
- **ToggleReaction**: Insert or remove reaction (toggle behavior — clicking same reaction removes it, different reaction replaces it)
- **TogglePin / ToggleLock / DeleteThread**: Admin moderation actions

### `FileService.cs`
Key responsibilities:
- **UploadAvatar**: Accept `IFormFile`, validate max 5MB, validate image type (jpg/png/gif/webp), save to `/app/uploads/avatars/{userId}.{ext}`, update user's `AvatarPath`, return URL
- **GetAvatarUrl**: Generate URL for a user's avatar (or null for default)
- Serve uploaded files via `UseStaticFiles()` or a dedicated endpoint

### `CacheService.cs`
```csharp
using StackExchange.Redis;
using System.Text.Json;

namespace Attrition.API.Services;

public class CacheService
{
    private readonly IDatabase _db;

    public CacheService(IConnectionMultiplexer redis)
    {
        _db = redis.GetDatabase();
    }

    public async Task<T?> GetAsync<T>(string key)
    {
        var value = await _db.StringGetAsync(key);
        return value.HasValue ? JsonSerializer.Deserialize<T>(value!) : default;
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? expiry = null)
    {
        var json = JsonSerializer.Serialize(value);
        await _db.StringSetAsync(key, json, expiry);
    }

    public async Task RemoveAsync(string key)
    {
        await _db.KeyDeleteAsync(key);
    }

    public async Task RemoveByPatternAsync(string pattern)
    {
        // Note: KEYS is not recommended for production with large datasets.
        // For this scale it's fine. Consider SCAN for larger deployments.
        var server = _db.Multiplexer.GetServer(_db.Multiplexer.GetEndPoints().First());
        var keys = server.Keys(pattern: pattern).ToArray();
        if (keys.Any()) await _db.KeyDeleteAsync(keys);
    }
}
```

---

## Middleware

### `ErrorHandlingMiddleware.cs`
```csharp
using System.Net;
using System.Text.Json;
using Attrition.API.DTOs;

namespace Attrition.API.Middleware;

public class ErrorHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ErrorHandlingMiddleware> _logger;

    public ErrorHandlingMiddleware(RequestDelegate next, ILogger<ErrorHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try { await _next(context); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception");
            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            context.Response.ContentType = "application/json";
            var response = new ApiResponse(false, "An unexpected error occurred");
            await context.Response.WriteAsync(JsonSerializer.Serialize(response));
        }
    }
}
```

---

## Validators

### `RegisterValidator.cs`
```csharp
using Attrition.API.DTOs;
using FluentValidation;

namespace Attrition.API.Validators;

public class RegisterValidator : AbstractValidator<RegisterRequest>
{
    public RegisterValidator()
    {
        RuleFor(x => x.Username)
            .NotEmpty().WithMessage("Username is required")
            .MinimumLength(3).WithMessage("Username must be at least 3 characters")
            .MaximumLength(30).WithMessage("Username must be at most 30 characters")
            .Matches("^[a-zA-Z0-9_]+$").WithMessage("Username can only contain letters, numbers, and underscores");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required")
            .MinimumLength(8).WithMessage("Password must be at least 8 characters")
            .Matches("[A-Z]").WithMessage("Password must contain at least one uppercase letter")
            .Matches("[a-z]").WithMessage("Password must contain at least one lowercase letter")
            .Matches("[0-9]").WithMessage("Password must contain at least one digit")
            .Matches("[^a-zA-Z0-9]").WithMessage("Password must contain at least one special character");
    }
}
```

---

## `appsettings.json`

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=attrition;Username=postgres;Password=postgres"
  },
  "Redis": {
    "ConnectionString": "localhost:6379"
  },
  "Jwt": {
    "Secret": "dev-secret-key-change-in-production-must-be-32-chars",
    "Issuer": "attrition-api",
    "Audience": "attrition-web",
    "AccessTokenExpiryMinutes": 15,
    "RefreshTokenExpiryDays": 7
  },
  "FileUpload": {
    "MaxAvatarSizeMB": 5,
    "UploadPath": "./uploads"
  }
}
```

---

## Next Step

Proceed to `04-DESIGN-SYSTEM.md` for the CSS design system.
