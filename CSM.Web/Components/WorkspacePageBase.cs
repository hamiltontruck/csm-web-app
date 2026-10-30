using CSM.Web.Services;
using CSM.Web.Services.Access;
using Microsoft.AspNetCore.Components;

namespace CSM.Web.Components;

public abstract class WorkspacePageBase : ComponentBase
{
    [Inject] protected IAuthService WorkspaceAuth { get; set; } = null!;
    protected WorkspaceAccess Access { get; private set; } = WorkspaceAccess.None;
    protected string? AccessError { get; private set; }
    protected bool AccessLoaded { get; private set; }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender) return;
        try
        {
            Access = new(await WorkspaceAuth.GetRolesAsync(), await WorkspaceAuth.GetPermissionsAsync());
            await OnAccessLoadedAsync();
        }
        catch
        {
            Access = WorkspaceAccess.None;
            AccessError = "Unable to load your access. Reload the page or sign in again.";
        }
        finally
        {
            AccessLoaded = true;
            await InvokeAsync(StateHasChanged);
        }
    }

    protected virtual Task OnAccessLoadedAsync() => Task.CompletedTask;
}
