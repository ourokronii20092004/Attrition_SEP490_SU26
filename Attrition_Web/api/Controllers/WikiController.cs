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

    // Admin: delete article
    [Authorize(Roles = "Admin")]
    [HttpDelete("articles/{id:guid}")]
    public async Task<IActionResult> DeleteArticle(Guid id) => Ok(await _wiki.DeleteArticleAsync(id));
}