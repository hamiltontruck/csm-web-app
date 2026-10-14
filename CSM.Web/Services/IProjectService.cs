using CSM.Web.Models.Projects;
using CSM.Web.Models.Sites;
using CSM.Web.Models.Equipment;


namespace CSM.Web.Services;

public interface IProjectService
{
    Task<List<ProjectSummary>> GetProjectsAsync();
    Task<ProjectDetail?> GetProjectAsync(Guid id);

    Task<bool> CreateProjectAsync(CreateProjectRequest request);

    Task<bool> UpdateProjectAsync(Guid projectId, UpdateProjectRequest request);

    Task<List<WorkTask>> GetWorkTasksAsync(Guid projectId);
    Task<(bool Success, string Message)> CreateTaskAsync(CreateWorkTaskRequest request);

    Task<List<ProjectPhase>> GetProjectPhasesAsync(Guid projectId);
    Task<bool> CreateProjectPhaseAsync(CreateProjectPhaseRequest request);
    Task<bool> UpdateProjectPhaseAsync(Guid phaseId, UpdateProjectPhaseRequest request);

    Task<List<ProjectMilestone>> GetProjectMilestonesAsync(Guid projectId);
    Task<bool> CreateProjectMilestoneAsync(CreateProjectMilestoneRequest request);

    Task<List<SiteResponse>> GetSitesAsync();

    Task<List<SiteResponse>> GetSitesForAssignmentAsync();



Task<List<ProjectResponse>> GetProjectsBySiteAsync(Guid constructionSiteId);

Task<List<DailyProgressLogResponse>> GetDailyProgressLogsAsync(Guid projectId);

Task CreateSiteAsync(CreateSiteRequest request);

Task<List<BusinessUnitModel>> GetBusinessUnitsAsync();

Task<SiteResponse> GetSiteAsync(Guid siteId);

Task UpdateSiteAsync(Guid siteId, UpdateSiteRequest request);

Task<SiteResponse> ChangeSiteStatusAsync(Guid siteId, SiteStatus status);

Task CreateDailyProgressAsync(CreateDailyProgressRequest request);

Task<List<DailyProgressLogResponse>> GetTaskDailyProgressAsync(Guid projectId);

Task<DailyProgressTaskEntryResponse> CreateTaskProgressEntryAsync(
    CreateDailyProgressTaskEntryRequest request);

Task<Guid> GetOrCreateDailyLogAsync(CreateDailyProgressRequest request);

Task<List<DailyProgressTaskEntryResponse>> GetTaskProgressEntriesAsync(Guid dailyLogId);

Task AddEquipmentEntryAsync(CreateEquipmentEntryRequest request);

Task<List<EquipmentSummary>> GetEquipmentAsync();
}