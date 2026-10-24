using CSM.Web.Services;

namespace CSM.Web.Services;

public sealed class PersonalDataExportService
{
    private readonly IHttpClientFactory _factory;
    private readonly IAuthService _authService;

    public PersonalDataExportService(
        IHttpClientFactory factory,
        IAuthService authService)
    {
        _factory = factory;
        _authService = authService;
    }

    public async Task<(byte[] Content, string FileName)> ExportAsync(
        CancellationToken cancellationToken = default)
    {
        var client = _factory.CreateClient("CSM.Api");
        await _authService.ApplyAuthenticationAsync(client);

        using var response = await client.GetAsync(
            "personal-data/export",
            cancellationToken);

        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsByteArrayAsync(
            cancellationToken);

        var fileName = response.Content.Headers.ContentDisposition?
            .FileNameStar
            ?? response.Content.Headers.ContentDisposition?.FileName
            ?? $"personal-data-{DateTime.UtcNow:yyyyMMdd-HHmmss}.json";

        fileName = fileName.Trim('"');

        return (content, fileName);
    }
}
