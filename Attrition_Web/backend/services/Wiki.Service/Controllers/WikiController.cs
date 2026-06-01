using BuildingBlocks.Authentication;
using BuildingBlocks.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wiki.Service.DTOs;
using Wiki.Service.Models;
using Wiki.Service.Services;

namespace Wiki.Service.Controllers;

[ApiController]
[Route("api/wiki")]
public class WikiController : ControllerBase
{
    private readonly IWikiService _wiki;
    private readonly ICurrentUser _currentUser;

    public WikiController(IWikiService wiki, ICurrentUser currentUser)
    {
        _wiki = wiki;
        _currentUser = currentUser;
    }

    // ─── Public reads ───
    [HttpGet("categories")]
    public async Task<IActionResult> GetCategories()
        => Ok(ApiResponse<List<WikiCategoryDto>>.Ok(await _wiki.GetCategoriesAsync()));

    [HttpGet("articles")]
    public async Task<IActionResult> GetArticles([FromQuery] string? category = null,
        [FromQuery] string? search = null, [FromQuery] Guid? authorId = null,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        => Ok(ApiResponse<PaginatedResponse<WikiArticleListDto>>.Ok(
            await _wiki.GetArticlesAsync(category, search, page, pageSize, authorId)));

    [HttpGet("articles/{slug}")]
    public async Task<IActionResult> GetArticle(string slug)
    {
        var article = await _wiki.GetArticleBySlugAsync(slug, _currentUser.IsAdmin);
        return article == null
            ? NotFound(ApiResponse.Fail("Article not found."))
            : Ok(ApiResponse<WikiArticleDto>.Ok(article));
    }

    [Authorize(Roles = Roles.Admin)]
    [HttpGet("articles/{id:guid}/revisions")]
    public async Task<IActionResult> GetRevisions(Guid id)
        => Ok(ApiResponse<List<WikiRevisionDto>>.Ok(await _wiki.GetRevisionsAsync(id)));

    [Authorize(Roles = Roles.Admin)]
    [HttpGet("articles/{id:guid}/revisions/{revisionId:guid}")]
    public async Task<IActionResult> GetRevision(Guid id, Guid revisionId)
    {
        var revision = await _wiki.GetRevisionByIdAsync(id, revisionId);
        return revision == null
            ? NotFound(ApiResponse.Fail("Revision not found."))
            : Ok(ApiResponse<WikiRevisionDto>.Ok(revision));
    }

    // ─── Authenticated: suggest edits ───
    [Authorize]
    [HttpPost("articles/{id:guid}/suggest")]
    public async Task<IActionResult> Suggest(Guid id, SuggestEditRequest request)
    {
        if (this.RequireUserId(_currentUser, out var userId) is { } error) return error;
        // Soft email-verification gate: unverified users may browse but not contribute.
        if (!_currentUser.IsEmailVerified)
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse.Fail("Please verify your email address before contributing."));
        var result = await _wiki.SubmitContributionAsync(id, request, userId, _currentUser.Username ?? "Unknown");
        return result.Success ? Ok(result) : BadRequest(result);
    }

    // ─── Admin ───
    [Authorize(Roles = Roles.Admin)]
    [HttpPost("articles")]
    public async Task<IActionResult> Create(CreateArticleRequest request)
    {
        if (this.RequireUserId(_currentUser, out var userId) is { } error) return error;
        var result = await _wiki.CreateArticleAsync(request, userId, _currentUser.Username ?? "Unknown");
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [Authorize(Roles = Roles.Admin)]
    [HttpPut("articles/{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpdateArticleRequest request)
    {
        if (this.RequireUserId(_currentUser, out var userId) is { } error) return error;
        var result = await _wiki.UpdateArticleAsync(id, request, userId, _currentUser.Username ?? "Unknown");
        return result.Success ? Ok(result) : NotFound(result);
    }

    [Authorize(Roles = Roles.Admin)]
    [HttpDelete("articles/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var result = await _wiki.DeleteArticleAsync(id);
        return result.Success ? Ok(result) : NotFound(result);
    }

    [Authorize(Roles = Roles.Admin)]
    [HttpGet("contributions")]
    public async Task<IActionResult> GetContributions([FromQuery] string status = ContributionStatus.Pending)
        => Ok(ApiResponse<List<WikiContributionDto>>.Ok(await _wiki.GetContributionsAsync(status)));

    [Authorize(Roles = Roles.Admin)]
    [HttpPost("contributions/{id:guid}/review")]
    public async Task<IActionResult> ReviewContribution(Guid id, ReviewContributionRequest request)
    {
        if (this.RequireUserId(_currentUser, out var userId) is { } error) return error;
        var result = await _wiki.ReviewContributionAsync(id, request, userId);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    // ─── Category management (admin) ───
    [Authorize(Roles = Roles.Admin)]
    [HttpPost("categories")]
    public async Task<IActionResult> CreateCategory(WikiCategoryRequest request)
    {
        var result = await _wiki.CreateCategoryAsync(request);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [Authorize(Roles = Roles.Admin)]
    [HttpPut("categories/{id:int}")]
    public async Task<IActionResult> UpdateCategory(int id, WikiCategoryRequest request)
    {
        var result = await _wiki.UpdateCategoryAsync(id, request);
        return result.Success ? Ok(result) : NotFound(result);
    }

    [Authorize(Roles = Roles.Admin)]
    [HttpDelete("categories/{id:int}")]
    public async Task<IActionResult> DeleteCategory(int id)
    {
        var (found, hasArticles) = await _wiki.DeleteCategoryAsync(id);
        if (!found) return NotFound(ApiResponse.Fail("Category not found."));
        if (hasArticles) return BadRequest(ApiResponse.Fail("Cannot delete a category that still has articles."));
        return Ok(ApiResponse.Ok());
    }
}
