using BuildingBlocks.Contracts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Reflection;

namespace BuildingBlocks.Web.Filters;

/// <summary>
/// Rejects requests where a [FromBody] parameter bound to null (empty body, wrong Content-Type,
/// malformed JSON). FluentValidation auto-validation does not run on a null model, so without this
/// the null reaches service code and throws → generic 500. Returns a 400 envelope instead.
/// </summary>
public sealed class RejectNullBodyFilter : IActionFilter
{
    public void OnActionExecuting(ActionExecutingContext context)
    {
        if (context.ActionDescriptor is not ControllerActionDescriptor descriptor)
            return;

        foreach (var parameter in descriptor.MethodInfo.GetParameters())
        {
            if (parameter.GetCustomAttribute<FromBodyAttribute>() is null)
                continue;

            var present = context.ActionArguments.TryGetValue(parameter.Name!, out var value);
            if (!present || value is null)
            {
                context.Result = new BadRequestObjectResult(
                    ApiResponse.Fail("Request body is required and cannot be empty."));
                return;
            }
        }
    }

    public void OnActionExecuted(ActionExecutedContext context) { }
}
