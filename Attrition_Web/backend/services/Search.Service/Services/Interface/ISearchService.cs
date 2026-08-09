using Search.Service.DTOs;

namespace Search.Service.Services.Interface;

public interface ISearchService
{
    Task<GlobalSearchResponse> GlobalSearchAsync(string query, int limit, bool includeUsers, CancellationToken ct);

    Task<List<SearchSuggestionDto>> SuggestAsync(string query, int limit, bool includeUsers, CancellationToken ct);
}