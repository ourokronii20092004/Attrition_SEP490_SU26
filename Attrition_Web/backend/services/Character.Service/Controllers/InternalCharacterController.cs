using BuildingBlocks.Authentication;
using BuildingBlocks.Contracts;
using Character.Service.DTOs;
using Character.Service.Services;
using Microsoft.AspNetCore.Mvc;

namespace Character.Service.Controllers;

/// <summary>
/// Game-client ingestion. The Unity client posts a character status snapshot on save/quit.
/// Guarded by the shared internal API key (X-Internal-Key), not user JWT, because the
/// trusted game server reports on behalf of players.
/// </summary>
[ApiController]
[Route("api/internal/characters")]
public class InternalCharacterController : ControllerBase
{
    private readonly ICharacterService _service;
    private readonly IConfiguration _config;

    public InternalCharacterController(ICharacterService service, IConfiguration config)
    {
        _service = service;
        _config = config;
    }

    private bool KeyValid() => InternalKey.Validate(Request, _config);

    [HttpPost("snapshot")]
    public async Task<IActionResult> Snapshot([FromBody] SnapshotIngestRequest request)
    {
        if (!KeyValid()) return Unauthorized(ApiResponse.Fail("Valid service authentication is required."));
        var result = await _service.IngestSnapshotAsync(request);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("count")]
    public async Task<IActionResult> Count()
    {
        if (!KeyValid()) return Unauthorized(ApiResponse.Fail("Valid service authentication is required."));
        return Ok(ApiResponse<int>.Ok(await _service.CountAsync()));
    }

    /// <summary>
    /// Host (trusted game server) đọc character detail (gồm inventoryJson) của BẤT KỲ player nào
    /// để nạp đồ cho nhân vật client trong coop. Dùng X-Internal-Key thay JWT — bỏ qua ownership
    /// guard ở endpoint player-facing (host cần đọc đồ của client, không phải của chính host).
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetDetail(Guid id)
    {
        if (!KeyValid()) return Unauthorized(ApiResponse.Fail("Valid service authentication is required."));
        var character = await _service.GetDetailAsync(id);
        return character == null
            ? NotFound(ApiResponse.Fail("Character not found."))
            : Ok(ApiResponse<CharacterDetailDto>.Ok(character));
    }
}
