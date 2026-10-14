namespace CSM.Application.Users.Dtos;

public sealed record SetUserRolesRequest(
    IReadOnlyCollection<string> Roles);