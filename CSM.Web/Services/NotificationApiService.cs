using System.Net.Http.Headers;
using System.Net.Http.Json;
using CSM.Web.Models.Notifications;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;

namespace CSM.Web.Services;

public sealed class NotificationApiService : INotificationApiService
{
    private readonly IHttpClientFactory _factory;
    private readonly ProtectedSessionStorage _storage;

    public NotificationApiService(
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

        if (token.Success && !string.IsNullOrWhiteSpace(token.Value))
        {
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token.Value);
        }

        return client;
    }

    public async Task<IReadOnlyList<NotificationResponse>> GetAsync(
        CancellationToken cancellationToken = default)
    {
        var client = await ClientAsync();

        return await client.GetFromJsonAsync<List<NotificationResponse>>(
            "Notifications",
            cancellationToken) ?? [];
    }

    public async Task<int> GetUnreadCountAsync(
        CancellationToken cancellationToken = default)
    {
        var client = await ClientAsync();

        return await client.GetFromJsonAsync<int>(
            "Notifications/unread-count",
            cancellationToken);
    }

    public async Task<NotificationResponse?> MarkReadAsync(
        Guid notificationId,
        bool isRead = true,
        CancellationToken cancellationToken = default)
    {
        var client = await ClientAsync();

        var response = await client.PatchAsJsonAsync(
            $"Notifications/{notificationId}/read",
            new
            {
                IsRead = isRead
            },
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(
                cancellationToken);

            throw new Exception(error);
        }

        return await response.Content
            .ReadFromJsonAsync<NotificationResponse>(
                cancellationToken: cancellationToken);
    }
}