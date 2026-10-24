
using System.Net.Http.Json;
using CSM.Web.Models.Security;

namespace CSM.Web.Services;

public sealed class AuditHistoryService
{
    private readonly IHttpClientFactory _factory;
    private readonly IAuthService _authService;

    public AuditHistoryService(
        IHttpClientFactory factory,
        IAuthService authService)
    {
        _factory = factory;
        _authService = authService;
    }

    public async Task<IReadOnlyList<AuditHistoryEntry>> GetLogsAsync(
        string? module = null,
        CancellationToken cancellationToken = default)
    {
        var client = _factory.CreateClient("CSM.Api");
        await _authService.ApplyAuthenticationAsync(client);

        var path = "audit";

        if (!string.IsNullOrWhiteSpace(module))
        {
            path += $"?module={Uri.EscapeDataString(module)}";
        }

        using var response = await client.GetAsync(
            path,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        return await response.Content
                   .ReadFromJsonAsync<List<AuditHistoryEntry>>(
                       cancellationToken: cancellationToken)
               ?? [];
    }
}

public sealed class AuditHistoryEntry
{
    public Guid Id { get; set; }
    public string? UserName { get; set; }
    public string? Module { get; set; }
    public string? Action { get; set; }
    public string? EntityName { get; set; }
    public string? Description { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
