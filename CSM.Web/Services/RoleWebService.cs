using System.Net.Http.Json;
using CSM.Web.Models.Roles;

namespace CSM.Web.Services;

public class RoleWebService
{
    private readonly HttpClient _http;
    private readonly IAuthService _auth;

    public RoleWebService(HttpClient http, IAuthService auth)
    {
        _http = http;
        _auth = auth;
    }

    private async Task AuthorizeAsync()
    {
        await _auth.ApplyAuthenticationAsync(_http);
    }

    public async Task<List<RoleSummaryModel>> GetRolesAsync()
    {
        await AuthorizeAsync();

        return await _http.GetFromJsonAsync<List<RoleSummaryModel>>("roles")
            ?? [];
    }

    public async Task<List<RolePermissionModel>> GetPermissionsAsync(Guid roleId)
    {
        await AuthorizeAsync();

        return await _http.GetFromJsonAsync<List<RolePermissionModel>>
            ($"roles/{roleId}/permissions")
            ?? [];
    }

    public async Task SavePermissionsAsync(
        Guid roleId,
        List<RolePermissionModel> permissions)
    {
        await AuthorizeAsync();

        var request = new UpdateRolePermissionsModel
        {
            Permissions = permissions
                .Where(x => x.Granted)
                .Select(x => x.Permission)
                .ToList()
        };

        var response = await _http.PutAsJsonAsync(
            $"roles/{roleId}/permissions",
            request);

        response.EnsureSuccessStatusCode();
    }
}