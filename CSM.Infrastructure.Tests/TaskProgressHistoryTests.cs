using System.Reflection;
using Bunit;
using CSM.Web.Components.Pages;
using CSM.Web.Models.Projects;
using CSM.Web.Services;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CSM.Infrastructure.Tests;

public sealed class TaskProgressHistoryTests
{
    private static readonly Guid ProjectId = Guid.NewGuid();
    private static readonly Guid TaskId = Guid.NewGuid();
    private static readonly Guid OlderLog = Guid.NewGuid();
    private static readonly Guid NewerLog = Guid.NewGuid();

    [Fact]
    public void Saved_task_entries_render_newest_first_with_dates_and_matching_resource_totals()
    {
        using var context = Context(true);
        var page = context.RenderComponent<TaskDetails>(p => p.Add(x => x.ProjectId, ProjectId).Add(x => x.TaskId, TaskId));
        page.WaitForAssertion(() => Assert.Contains("Supervisor test: foundation work progressed to 20%.", page.Markup));
        Assert.Contains("Previous foundation work", page.Markup);
        Assert.Contains("23 Oct 2026", page.Markup);
        Assert.Contains("18 Oct 2026", page.Markup);
        Assert.DoesNotContain("No Progress Recorded", page.Markup);
        Assert.DoesNotContain("Other task private work", page.Markup);
        Assert.True(page.Markup.IndexOf("Supervisor test:", StringComparison.Ordinal) <
            page.Markup.IndexOf("Previous foundation work", StringComparison.Ordinal));
        Assert.Contains(">5</h3>", page.Markup);
        Assert.Contains("20%", page.Markup);
        Assert.Contains("Labour: 3", page.Markup);
        Assert.Contains("Equipment: 1", page.Markup);
        Assert.Contains("Hours: 2.5", page.Markup);
    }

    [Fact]
    public void Task_without_entries_keeps_empty_state_even_when_other_tasks_have_entries()
    {
        using var context = Context(false);
        var page = context.RenderComponent<TaskDetails>(p => p.Add(x => x.ProjectId, ProjectId).Add(x => x.TaskId, TaskId));
        page.WaitForAssertion(() => Assert.Contains("No Progress Recorded", page.Markup));
        Assert.DoesNotContain("Other task private work", page.Markup);
        Assert.DoesNotContain("Supervisor test:", page.Markup);
    }

    private static TestContext Context(bool entries)
    {
        var context = new TestContext();
        var service = DispatchProxy.Create<IProjectService, ProjectProxy>();
        ((ProjectProxy)(object)service).IncludeTaskEntries = entries;
        context.Services.AddSingleton(service);
        return context;
    }

    public class ProjectProxy : DispatchProxy
    {
        public bool IncludeTaskEntries { get; set; }
        protected override object? Invoke(MethodInfo? method, object?[]? args) => method!.Name switch
        {
            "GetWorkTasksAsync" => Task.FromResult(new List<WorkTask> {
                new() { Id = TaskId, Title = "Foundation Concrete Work", TaskNumber = "WT-001",
                    ProgressPercentage = 20, Status = "InProgress" } }),
            "GetTaskDailyProgressAsync" => Task.FromResult(new List<DailyProgressLogResponse> {
                new() { Id = OlderLog, ProjectId = ProjectId, LogDate = new DateTime(2026, 10, 18) },
                new() { Id = NewerLog, ProjectId = ProjectId, LogDate = new DateTime(2026, 10, 23) } }),
            "GetTaskProgressEntriesAsync" => Task.FromResult(Entries((Guid)args![0]!)),
            _ => throw new NotSupportedException("History rendering must only read: " + method.Name)
        };

        private List<DailyProgressTaskEntryResponse> Entries(Guid logId)
        {
            var entries = new List<DailyProgressTaskEntryResponse> {
                new() { Id = Guid.NewGuid(), WorkTaskId = Guid.NewGuid(),
                    WorkCompleted = "Other task private work", LabourCount = 99 } };
            if (IncludeTaskEntries)
                entries.Add(new() { Id = Guid.NewGuid(), WorkTaskId = TaskId,
                    ProgressPercentage = logId == NewerLog ? 20 : 10,
                    LabourCount = logId == NewerLog ? 3 : 2,
                    EquipmentCount = logId == NewerLog ? 1 : 0,
                    EquipmentHours = logId == NewerLog ? 2.5m : 0,
                    WorkCompleted = logId == NewerLog
                        ? "Supervisor test: foundation work progressed to 20%." : "Previous foundation work" });
            return entries;
        }
    }
}
