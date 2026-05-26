using Attrition.API.DTOs;
using Attrition.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Attrition.API.Models;

namespace Attrition.API.Controllers;

[ApiController]
[Route("api/wiki")]
public class WikiController : ControllerBase
{
    private readonly IWikiService _wiki;
    public WikiController(IWikiService wiki) => _wiki = wiki;

    [HttpGet("categories")]
    public async Task<IActionResult> GetCategories() 
        => Ok(new ApiResponse<List<WikiCategoryDto>>(true, await _wiki.GetCategoriesAsync()));

    [HttpGet("articles")]
    public async Task<IActionResult> GetArticles([FromQuery] string? category, [FromQuery] string? search,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        => Ok(new ApiResponse<PaginatedResponse<WikiArticleListDto>>(true, await _wiki.GetArticlesAsync(category, search, page, pageSize)));

    [HttpGet("articles/{slug}")]
    public async Task<IActionResult> GetArticle(string slug)
    {
        var result = await _wiki.GetArticleBySlugAsync(slug);
        return result != null ? Ok(new ApiResponse<WikiArticleDto>(true, result)) : NotFound(new ApiResponse(false, "Article not found"));
    }

    [HttpGet("articles/{id:guid}/revisions")]
    public async Task<IActionResult> GetRevisions(Guid id) 
        => Ok(new ApiResponse<List<WikiRevision>>(true, await _wiki.GetRevisionsAsync(id)));

    [HttpGet("articles/{id:guid}/revisions/{revisionId:guid}")]
    public async Task<IActionResult> GetRevision(Guid id, Guid revisionId)
    {
        var revision = await _wiki.GetRevisionByIdAsync(id, revisionId);
        return revision != null ? Ok(new ApiResponse<WikiRevision>(true, revision)) : NotFound(new ApiResponse(false, "Revision not found"));
    }


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
        => Ok(new ApiResponse<List<WikiContributionDto>>(true, await _wiki.GetContributionsAsync(status)));

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
    public async Task<IActionResult> DeleteArticle(Guid id)
    {
        var result = await _wiki.DeleteArticleAsync(id);
        return result.Success ? Ok(result) : BadRequest(result);
    }
}