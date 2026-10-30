using CSM.Application.Dashboard.Dtos;
namespace CSM.Application.Dashboard;

public interface IWorkspaceDashboardService
{
    Task<WorkspaceDashboardResponse> GetAsync(Guid actorId, Guid? selectedCompanyId = null, CancellationToken cancellationToken = default);
}
