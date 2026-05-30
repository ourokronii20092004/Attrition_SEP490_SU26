using BuildingBlocks.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace BuildingBlocks.Web;

/// <summary>
/// Replaces ASP.NET's default ProblemDetails response for model-validation failures with the
/// <see cref="ApiResponse"/> envelope used everywhere else, so clients only ever parse one error shape.
/// </summary>
public static class ValidationResponseFactory
{
    public static IActionResult Create(ActionContext context)
    {
        var message = string.Join("; ", context.ModelState
            .Where(kvp => kvp.Value?.Errors.Count > 0)
            .SelectMany(kvp => kvp.Value!.Errors.Select(err =>
                string.IsNullOrEmpty(kvp.Key) ? err.ErrorMessage : $"{kvp.Key}: {err.ErrorMessage}")));

        var response = ApiResponse.Fail(
            string.IsNullOrWhiteSpace(message) ? "One or more validation errors occurred." : message);

        return new BadRequestObjectResult(response);
    }
}
