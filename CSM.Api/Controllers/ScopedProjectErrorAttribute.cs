using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
namespace CSM.Api.Controllers;
[AttributeUsage(AttributeTargets.Class)]
public sealed class ScopedProjectErrorAttribute : ExceptionFilterAttribute
{
    public override void OnException(ExceptionContext context)
    {
        if (context.Exception is UnauthorizedAccessException)
            context.Result = new ObjectResult(new { message = "Access denied." }) { StatusCode = 403 };
        else if (context.Exception is ArgumentException)
            context.Result = new BadRequestObjectResult(new { message = "Invalid project data." });
        else return;
        context.ExceptionHandled = true;
    }
}
