using System.Reflection;
using Bunit;
using CSM.Web.Components;
using CSM.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
namespace CSM.Infrastructure.Tests;
public sealed class OfflineSessionKeyRecoveryTests
{
    [Fact]
    public async Task Old_session_key_recovers_to_sign_in_without_configuring_or_running_offline_sync()
    {
        string encrypted;
        using (var old=Context(out _))
        {
            await old.Services.GetRequiredService<ProtectedSessionStorage>().SetAsync("jwt","local-old-token");
            encrypted=(string)old.JSInterop.Invocations.Single(x=>x.Identifier=="sessionStorage.setItem").Arguments[1]!;
        }
        using var c=Context(out var offline);c.JSInterop.Setup<string>("sessionStorage.getItem",_=>true).SetResult(encrypted);
        c.RenderComponent<OfflineSyncInitializer>();
        Assert.EndsWith("/login",c.Services.GetRequiredService<NavigationManager>().Uri);
        Assert.Equal(0,offline.Initializations);Assert.Empty(c.JSInterop.Invocations.Where(x=>x.Identifier=="csmOfflineSync.configureApi"));
        Assert.Empty(c.JSInterop.Invocations.Where(x=>x.Identifier.Contains("remove",StringComparison.OrdinalIgnoreCase)||x.Identifier.Contains("clear",StringComparison.OrdinalIgnoreCase)));
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task Current_or_missing_session_keeps_connection_monitoring(bool hasToken)
    {
        using var c=Context(out var offline);string? encrypted=null;
        if(hasToken){await c.Services.GetRequiredService<ProtectedSessionStorage>().SetAsync("jwt","local-current-token");encrypted=(string)c.JSInterop.Invocations.Single(x=>x.Identifier=="sessionStorage.setItem").Arguments[1]!;}
        c.JSInterop.Setup<string?>("sessionStorage.getItem",_=>true).SetResult(encrypted);
        c.RenderComponent<OfflineSyncInitializer>();Assert.Equal(1,offline.Initializations);
        var calls=c.JSInterop.Invocations.Where(x=>x.Identifier=="csmOfflineSync.configureApi").ToArray();Assert.Equal(hasToken?1:0,calls.Length);
        if(hasToken)Assert.Equal("local-current-token",calls.Single().Arguments[1]);
    }
    private static TestContext Context(out Offline offline)
    {
        var c=new TestContext();c.JSInterop.Mode=JSRuntimeMode.Loose;c.Services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());c.Services.AddScoped<ProtectedSessionStorage>();
        c.Services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{{"ApiSettings:BaseUrl","https://fixture.test/api/"}}).Build());
        var service=DispatchProxy.Create<IOfflineSyncService,Offline>();offline=(Offline)(object)service;c.Services.AddSingleton(service);c.Services.AddSingleton(DispatchProxy.Create<IOfflineSyncCoordinator,Coordinator>());return c;
    }
    public class Offline:DispatchProxy
    {
        public int Initializations;protected override object? Invoke(MethodInfo? m,object?[]? a){if(m!.Name.StartsWith("add_")||m.Name.StartsWith("remove_"))return null;if(m.Name==nameof(IOfflineSyncService.InitializeConnectionMonitoringAsync)){Initializations++;return Task.CompletedTask;}throw new InvalidOperationException("Unexpected offline operation");}
    }
    public class Coordinator:DispatchProxy{protected override object? Invoke(MethodInfo? m,object?[]? a)=>throw new InvalidOperationException("Recovery must not synchronize queued business records.");}
}
