using System.Reflection;
using Bunit;
using CSM.Web.Components.Pages;
using CSM.Web.Models.Equipment;
using CSM.Web.Models.Projects;
using CSM.Web.Services;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CSM.Infrastructure.Tests;

public sealed class DailyProgressFormTests
{
    [Fact]
    public void Equipment_failure_does_not_block_reporting_without_equipment()
    {
        using var context = Context();
        var proxy = (ProjectProxy)(object)context.Services.GetRequiredService<IProjectService>();
        proxy.EquipmentError = true;
        var page = context.RenderComponent<CreateDailyProgress>(p => p.Add(x => x.ProjectId, ProjectId));
        page.WaitForAssertion(() => Assert.Contains("equipment", page.Find("[role=alert]").TextContent.ToLowerInvariant()));
        Fill(page);
        page.Find("form").Submit();
        page.WaitForAssertion(() => Assert.Single(proxy.Entries));
        var entry = proxy.Entries.Single();
        Assert.Equal(10m, entry.ProgressPercentage);
        Assert.Equal(2, entry.LabourCount);
        Assert.Equal("Concrete work", entry.WorkCompleted);
    }

    [Fact]
    public void Save_failure_preserves_values_and_allows_retry()
    {
        using var context = Context();
        var proxy = (ProjectProxy)(object)context.Services.GetRequiredService<IProjectService>();
        proxy.SaveError = true;
        var page = context.RenderComponent<CreateDailyProgress>(p => p.Add(x => x.ProjectId, ProjectId));
        Fill(page);
        page.Find("form").Submit();
        page.WaitForAssertion(() => Assert.Contains("Unable to save", page.Find("[role=alert]").TextContent));
        Assert.Equal("Concrete work", page.Find("textarea").GetAttribute("value") ?? page.Find("textarea").TextContent);
        Assert.False(page.Find("button[type=submit]").HasAttribute("disabled"));
        proxy.SaveError = false;
        page.Find("form").Submit();
        page.WaitForAssertion(() => Assert.Single(proxy.Entries));
    }

    private static void Fill(IRenderedComponent<CreateDailyProgress> page)
    {
        page.FindAll("input").ToArray()[1].Change("10");
        page.FindAll("input").ToArray()[2].Change("2");
        page.Find("textarea").Change("Concrete work");
    }

    private static readonly Guid ProjectId = Guid.NewGuid();
    private static TestContext Context()
    {
        var context = new TestContext();
        context.Services.AddSingleton(DispatchProxy.Create<IProjectService, ProjectProxy>());
        return context;
    }

    public class ProjectProxy : DispatchProxy
    {
        public bool EquipmentError { get; set; }
        public bool SaveError { get; set; }
        public List<CreateDailyProgressTaskEntryRequest> Entries { get; } = [];
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            return method!.Name switch {
                "GetEquipmentAsync" => EquipmentError
                    ? Task.FromException<List<EquipmentSummary>>(new HttpRequestException("403 Forbidden"))
                    : Task.FromResult(new List<EquipmentSummary>()),
                "GetOrCreateDailyLogAsync" => SaveError
                    ? Task.FromException<Guid>(new HttpRequestException("Service unavailable"))
                    : Task.FromResult(Guid.NewGuid()),
                "CreateTaskProgressEntryAsync" => Entry((CreateDailyProgressTaskEntryRequest)args![0]!),
                _ => throw new NotSupportedException(method.Name)
            };
        }
        private Task<DailyProgressTaskEntryResponse> Entry(CreateDailyProgressTaskEntryRequest request)
        {
            Entries.Add(request);
            return Task.FromResult(new DailyProgressTaskEntryResponse { Id = Guid.NewGuid() });
        }
    }
}
