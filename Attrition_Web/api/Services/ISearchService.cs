using Attrition.API.DTOs;

namespace Attrition.API.Services;

public interface ISearchService
{
    Task<GlobalSearchResponse> GlobalSearchAsync(string q);
}
