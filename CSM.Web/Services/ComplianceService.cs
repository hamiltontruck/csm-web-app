using System.Net.Http.Headers;
using System.Net.Http.Json;
using CSM.Web.Models.Compliance;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Microsoft.AspNetCore.Components.Forms;


namespace CSM.Web.Services;

public sealed class ComplianceService : IComplianceService
{
    private readonly IHttpClientFactory _factory;
    private readonly ProtectedSessionStorage _storage;

    public ComplianceService(
        IHttpClientFactory factory,
        ProtectedSessionStorage storage)
    {
        _factory = factory;
        _storage = storage;
    }

    private async Task<HttpClient> ClientAsync()
    {
        var client = _factory.CreateClient("CSM.Api");

        var token = await _storage.GetAsync<string>("jwt");

        if (token.Success &&
            !string.IsNullOrWhiteSpace(token.Value))
        {
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue(
                    "Bearer",
                    token.Value);
        }

        return client;
    }

    public async Task<IReadOnlyList<DocumentCategoryResponse>>
        GetDocumentCategoriesAsync(bool? isActive = null)
    {
        var client = await ClientAsync();

        var url = "DocumentCategories";

        if (isActive.HasValue)
        {
            url += $"?isActive={isActive.Value.ToString().ToLowerInvariant()}";
        }

        return await client.GetFromJsonAsync<
            List<DocumentCategoryResponse>>(url) ?? [];
    }

    public async Task<DocumentCategoryResponse?>
        GetDocumentCategoryAsync(Guid id)
    {
        var client = await ClientAsync();

        var response = await client.GetAsync(
            $"DocumentCategories/{id}");

        if (response.StatusCode ==
            System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        return await response.Content
            .ReadFromJsonAsync<DocumentCategoryResponse>();
    }

    public async Task<DocumentCategoryResponse>
        CreateDocumentCategoryAsync(
            CreateDocumentCategoryRequest request)
    {
        var client = await ClientAsync();

        var response = await client.PostAsJsonAsync(
            "DocumentCategories",
            request);

        if (!response.IsSuccessStatusCode)
        {
            var error =
                await response.Content.ReadAsStringAsync();

            throw new InvalidOperationException(
                $"Failed to create document category. " +
                $"Status: {(int)response.StatusCode}. {error}");
        }

        return await response.Content
                   .ReadFromJsonAsync<DocumentCategoryResponse>()
               ?? throw new InvalidOperationException(
                   "The API returned an empty document category response.");
    }

    public async Task<DocumentCategoryResponse>
        UpdateDocumentCategoryAsync(
            Guid id,
            UpdateDocumentCategoryRequest request)
    {
        var client = await ClientAsync();

        var response = await client.PutAsJsonAsync(
            $"DocumentCategories/{id}",
            request);

        if (!response.IsSuccessStatusCode)
        {
            var error =
                await response.Content.ReadAsStringAsync();

            throw new InvalidOperationException(
                $"Failed to update document category. " +
                $"Status: {(int)response.StatusCode}. {error}");
        }

        return await response.Content
                   .ReadFromJsonAsync<DocumentCategoryResponse>()
               ?? throw new InvalidOperationException(
                   "The API returned an empty document category response.");
    }

    public async Task<IReadOnlyList<DocumentResponse>>
        GetDocumentsAsync()
    {
        var client = await ClientAsync();

        return await client.GetFromJsonAsync<
            List<DocumentResponse>>("Documents") ?? [];
    }

    public async Task<DocumentResponse?>
        GetDocumentAsync(Guid id)
    {
        var client = await ClientAsync();

        var response = await client.GetAsync(
            $"Documents/{id}");

        if (response.StatusCode ==
            System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        return await response.Content
            .ReadFromJsonAsync<DocumentResponse>();
    }

    public async Task<DocumentResponse>
        CreateDocumentAsync(
            CreateDocumentRequest request)
    {
        var client = await ClientAsync();

        var response = await client.PostAsJsonAsync(
            "Documents",
            request);

        if (!response.IsSuccessStatusCode)
        {
            var error =
                await response.Content.ReadAsStringAsync();

            throw new InvalidOperationException(
                $"Failed to create document. " +
                $"Status: {(int)response.StatusCode}. {error}");
        }

        return await response.Content
                   .ReadFromJsonAsync<DocumentResponse>()
               ?? throw new InvalidOperationException(
                   "The API returned an empty document response.");
    }

    public async Task<DocumentResponse>
        UpdateDocumentAsync(
            Guid id,
            UpdateDocumentRequest request)
    {
        var client = await ClientAsync();

        var response = await client.PutAsJsonAsync(
            $"Documents/{id}",
            request);

        if (!response.IsSuccessStatusCode)
        {
            var error =
                await response.Content.ReadAsStringAsync();

            throw new InvalidOperationException(
                $"Failed to update document. " +
                $"Status: {(int)response.StatusCode}. {error}");
        }

        return await response.Content
                   .ReadFromJsonAsync<DocumentResponse>()
               ?? throw new InvalidOperationException(
                   "The API returned an empty document response.");
    }

    public async Task<DocumentVersionResponse> UploadDocumentVersionAsync(
        Guid documentId,
        IBrowserFile file,
        string? revisionNotes)
    {
        var client = await ClientAsync();

        using var content = new MultipartFormDataContent();

        await using var stream = file.OpenReadStream(100_000_000);
        using var fileContent = new StreamContent(stream);

        if (!string.IsNullOrWhiteSpace(file.ContentType))
        {
            fileContent.Headers.ContentType =
                new MediaTypeHeaderValue(file.ContentType);
        }

        content.Add(fileContent, "File", file.Name);
        content.Add(
            new StringContent(revisionNotes ?? string.Empty),
            "RevisionNotes");

        var response = await client.PostAsync(
            $"Documents/{documentId}/upload-version",
            content);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();

            throw new InvalidOperationException(
                $"Failed to upload document version. Status: {(int)response.StatusCode}. {error}");
        }

        return await response.Content.ReadFromJsonAsync<DocumentVersionResponse>()
            ?? throw new InvalidOperationException(
                "The API returned an empty document version response.");
    }

    public async Task<IReadOnlyList<DocumentVersionResponse>> GetDocumentVersionsAsync(
        Guid documentId)
    {
        var client = await ClientAsync();

        return await client.GetFromJsonAsync<List<DocumentVersionResponse>>(
            $"documents/{documentId}/versions") ?? [];
    }

    public async Task<(byte[] Content, string FileName, string ContentType)>
        DownloadDocumentVersionAsync(
            Guid documentId,
            Guid versionId)
    {
        var client = await ClientAsync();

        var response = await client.GetAsync(
            $"documents/{documentId}/versions/{versionId}/download");

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();

            throw new InvalidOperationException(
                $"Failed to download document version. " +
                $"Status: {(int)response.StatusCode}. {error}");
        }

        var content = await response.Content.ReadAsByteArrayAsync();

        var fileName =
            response.Content.Headers.ContentDisposition?.FileNameStar
            ?? response.Content.Headers.ContentDisposition?.FileName
            ?? $"document-version-{versionId}";

        fileName = fileName.Trim('"');

        var contentType =
            response.Content.Headers.ContentType?.MediaType
            ?? "application/octet-stream";

        return (content, fileName, contentType);
    }

    public async Task<IReadOnlyList<SafetyIncidentResponse>>
        GetSafetyIncidentsAsync()
    {
        var client = await ClientAsync();

        return await client.GetFromJsonAsync<List<SafetyIncidentResponse>>(
            "SafetyIncidents") ?? [];
    }

    public async Task<SafetyIncidentResponse?>
        GetSafetyIncidentAsync(Guid id)
    {
        var client = await ClientAsync();

        return await client.GetFromJsonAsync<SafetyIncidentResponse>(
            $"SafetyIncidents/{id}");
    }

    public async Task<SafetyIncidentResponse>
        CreateSafetyIncidentAsync(
            CreateSafetyIncidentRequest request)
    {
        var client = await ClientAsync();

        var response = await client.PostAsJsonAsync(
            "SafetyIncidents",
            request);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();

            throw new InvalidOperationException(
                $"Failed to create safety incident. " +
                $"Status: {(int)response.StatusCode}. {error}");
        }

        return await response.Content
            .ReadFromJsonAsync<SafetyIncidentResponse>()
            ?? throw new InvalidOperationException(
                "The API returned an empty safety incident response.");
    }

    public async Task<SafetyIncidentResponse>
        UpdateSafetyIncidentAsync(
            Guid id,
            UpdateSafetyIncidentRequest request)
    {
        var client = await ClientAsync();

        var response = await client.PutAsJsonAsync(
            $"SafetyIncidents/{id}",
            request);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();

            throw new InvalidOperationException(
                $"Failed to update safety incident. " +
                $"Status: {(int)response.StatusCode}. {error}");
        }

        return await response.Content
            .ReadFromJsonAsync<SafetyIncidentResponse>()
            ?? throw new InvalidOperationException(
                "The API returned an empty safety incident response.");
    }
}