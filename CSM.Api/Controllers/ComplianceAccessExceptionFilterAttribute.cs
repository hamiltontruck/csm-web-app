using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
namespace CSM.Api.Controllers;

[AttributeUsage(AttributeTargets.Class)]
public sealed class ComplianceAccessExceptionFilterAttribute : ExceptionFilterAttribute
{
    public override void OnException(ExceptionContext context)
    {
        if (context.Exception is UnauthorizedAccessException)
            context.Result = new ForbidResult();
        else if (context.Exception is KeyNotFoundException)
            context.Result = new NotFoundObjectResult(new { message = "Compliance record or related resource was not found." });
        else return;
        context.ExceptionHandled = true;
    }
}
