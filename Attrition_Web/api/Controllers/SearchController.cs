using Attrition.API.DTOs;
using Attrition.API.Services;
using Microsoft.AspNetCore.Mvc;

namespace Attrition.API.Controllers;

[ApiController]
[Route("api/search")]
public class SearchController : ControllerBase
{
    private readonly ISearchService _search;
    public SearchController(ISearchService search) => _search = search;

    [HttpGet]
    public async Task<IActionResult> GlobalSearch([FromQuery] string q)
        => Ok(new ApiResponse<GlobalSearchResponse>(true, await _search.GlobalSearchAsync(q)));
}
