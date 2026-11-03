using System.Reflection;
using Bunit;
using CSM.Web.Components.Layout;
using CSM.Web.Models.Notifications;
using CSM.Web.Services;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using NotificationPage = CSM.Web.Components.Pages.Notifications;

namespace CSM.Infrastructure.Tests;

public sealed class NotificationBadgeTests
{
    [Theory]
    [InlineData(1, 0)]
    [InlineData(2, 1)]
    public void Marking_one_notification_read_refreshes_the_navigation_badge(int initial, int expected)
    {
        using var context = Context(initial, out var api);
        var menu = context.RenderComponent<NavMenu>();
        menu.WaitForAssertion(() => Assert.Equal(initial.ToString(), menu.Find("a[href='/notifications'] .badge").TextContent.Trim()));
        var page = context.RenderComponent<NotificationPage>();
        page.WaitForAssertion(() => Assert.Equal(initial, page.FindAll("button").Count));
        page.Find("button").Click();
        page.WaitForAssertion(() => Assert.Equal(expected, page.FindAll("button").Count));
        menu.WaitForAssertion(() =>
        {
            if (expected == 0) Assert.Empty(menu.FindAll("a[href='/notifications'] .badge"));
            else Assert.Equal(expected.ToString(), menu.Find("a[href='/notifications'] .badge").TextContent.Trim());
        });
    }

    [Fact]
    public void Failed_mark_read_does_not_clear_the_unread_badge()
    {
        using var context = Context(1, out var api);
        api.FailMark = true;
        var menu = context.RenderComponent<NavMenu>();
        menu.WaitForAssertion(() => Assert.Equal("1", menu.Find("a[href='/notifications'] .badge").TextContent.Trim()));
        var page = context.RenderComponent<NotificationPage>();
        page.WaitForAssertion(() => Assert.Single(page.FindAll("button")));
        page.Find("button").Click();
        page.WaitForAssertion(() => Assert.Single(page.FindAll(".alert-danger")));
        Assert.Equal("1", menu.Find("a[href='/notifications'] .badge").TextContent.Trim());
    }

    [Fact]
    public void A_slow_initial_count_cannot_restore_a_badge_after_mark_read()
    {
        using var context = Context(1, out var api);
        api.InitialCount = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var menu = context.RenderComponent<NavMenu>();
        var page = context.RenderComponent<NotificationPage>();
        page.WaitForAssertion(() => Assert.Single(page.FindAll("button")));
        page.Find("button").Click();
        api.InitialCount.SetResult(1);
        menu.WaitForAssertion(() => Assert.Empty(menu.FindAll("a[href='/notifications'] .badge")));
        // Flush queued renders after completing the delayed initial request.
        menu.InvokeAsync(() => { }).GetAwaiter().GetResult();
        menu.WaitForAssertion(() => Assert.Empty(menu.FindAll("a[href='/notifications'] .badge")));
    }

    internal static void RegisterRefreshState(IServiceCollection services)
    {
        var type = typeof(NavMenu).Assembly.GetType("CSM.Web.Services.NotificationReadState");
        if (type is not null) services.AddScoped(type);
    }

    private static TestContext Context(int count, out Api api)
    {
        var context = new TestContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        api = new Api(count);
        context.Services.AddSingleton<INotificationApiService>(api);
        context.Services.AddSingleton(DispatchProxy.Create<IAuthService, Auth>());
        RegisterRefreshState(context.Services);
        return context;
    }

    public class Auth : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? method, object?[]? args) => method!.Name switch
        {
            nameof(IAuthService.GetRolesAsync) => Task.FromResult<IReadOnlyCollection<string>>(new[] { "SiteManager" }),
            nameof(IAuthService.GetPermissionsAsync) => Task.FromResult<IReadOnlyCollection<string>>(new[] { "Notifications.View" }),
            _ => throw new InvalidOperationException(method.Name)
        };
    }

    public sealed class Api(int count) : INotificationApiService
    {
        private readonly List<NotificationResponse> rows = Enumerable.Range(0, count).Select(_ =>
            new NotificationResponse { Id = Guid.NewGuid(), Title = "Test notification", Message = "Test message", Severity = "Info",
                CreatedAtUtc = new DateTime(2026, 11, 3, 0, 0, 0, DateTimeKind.Utc) }).ToList();
        private int calls;
        public bool FailMark;
        public TaskCompletionSource<int>? InitialCount;

        public Task<int> GetUnreadCountAsync(CancellationToken cancellationToken = default) =>
            Interlocked.Increment(ref calls) == 1 && InitialCount is not null
                ? InitialCount.Task : Task.FromResult(rows.Count(x => !x.IsRead));

        public Task<IReadOnlyList<NotificationResponse>> GetAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<NotificationResponse>>(rows.Select(x => new NotificationResponse {
                Id = x.Id, Title = x.Title, Message = x.Message, Severity = x.Severity, IsRead = x.IsRead,
                ReadAtUtc = x.ReadAtUtc, CreatedAtUtc = x.CreatedAtUtc }).ToList());

        public Task<NotificationResponse?> MarkReadAsync(Guid notificationId, bool isRead = true, CancellationToken cancellationToken = default)
        {
            if (FailMark) return Task.FromException<NotificationResponse?>(new HttpRequestException("Test read failure."));
            var row = rows.Single(x => x.Id == notificationId);
            row.IsRead = isRead;
            row.ReadAtUtc = DateTime.UtcNow;
            return Task.FromResult<NotificationResponse?>(row);
        }
    }
}
