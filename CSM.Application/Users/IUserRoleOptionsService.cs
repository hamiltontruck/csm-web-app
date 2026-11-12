namespace CSM.Application.Users;

public interface IUserRoleOptionsService
{
    Task<IReadOnlyCollection<string>> GetAssignableRolesAsync(Guid actorId, CancellationToken cancellationToken = default);
}
