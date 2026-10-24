using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;

namespace CSM.Api.Controllers;

public abstract class CurrentUserControllerBase : ControllerBase
{
    protected Guid CurrentUserId
    {
        get
        {
            var userIdValue =
                User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (!Guid.TryParse(userIdValue, out var userId))
            {
                throw new UnauthorizedAccessException(
                    "Authenticated user identifier is invalid.");
            }

            return userId;
        }
    }
}
