namespace CSM.Web.Services;

/// <summary>Coordinates read-state refreshes within the current user circuit.</summary>
public sealed class NotificationReadState
{
    public event Func<Task>? Changed;

    public async Task NotifyChangedAsync()
    {
        var handlers = Changed;
        if (handlers is null) return;
        foreach (Func<Task> handler in handlers.GetInvocationList())
            await handler();
    }
}
