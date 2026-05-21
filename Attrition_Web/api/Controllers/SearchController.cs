using Attrition.API.Services;
using Microsoft.AspNetCore.Mvc;

namespace Attrition.API.Controllers;

[ApiController]
[Route("api/search")]
public class SearchController : ControllerBase
{
    private readonly SearchService _search;
    public SearchController(SearchService search) => _search = search;

    [HttpGet]
    public async Task<IActionResult> GlobalSearch([FromQuery] string q)
        => Ok(await _search.GlobalSearchAsync(q));
}
