using Bunit;
using CSM.Domain.Enums;
using CSM.Web.Services.Access;
using Microsoft.AspNetCore.Components;

namespace CSM.Infrastructure.Tests;

// Standalone page tests supply the same authorized cascading context as the
// production route gate. The real action component and policy still render.
internal static class WorkspaceTestAccess
{
    public static void Add(TestContext context, string[]? roles = null, string[]? permissions = null)
    {
        var access = new WorkspaceAccess(roles ?? ["CompanyAdmin"],
            permissions ?? Enum.GetNames<GranularPermission>());
        context.RenderTree.Add<CascadingValue<WorkspaceAccess>>(p => p.Add(x => x.Value, access));
    }
}
