using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CSM.Web.Models.OfflineSync;
using CSM.Web.Models.Finance;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;

namespace CSM.Web.Services;

public sealed class OfflineSyncApi : IOfflineSyncApi
{
    private readonly IHttpClientFactory _factory;
    private readonly ProtectedSessionStorage _storage;

    public OfflineSyncApi(
        IHttpClientFactory factory,
        ProtectedSessionStorage storage)
    {
        _factory = factory;
        _storage = storage;
    }

    public async Task ExecuteAsync(
        OfflineSyncOperation operation,
        CancellationToken cancellationToken = default)
    {
        var client = await ClientAsync();

        if (string.Equals(
                operation.EntityType,
                "CostCode",
                StringComparison.OrdinalIgnoreCase))
        {
            await ExecuteCostCodeAsync(
                client,
                operation,
                cancellationToken);

            return;
        }

        throw new NotSupportedException(
            $"Offline synchronization is not supported for entity type '{operation.EntityType}'.");
    }

    private async Task ExecuteCostCodeAsync(
        HttpClient client,
        OfflineSyncOperation operation,
        CancellationToken cancellationToken)
    {
        switch (operation.OperationType)
        {
            case OfflineSyncOperationType.Create:
            {
                var request =
                    JsonSerializer.Deserialize<CreateCostCodeResponse>(
                        operation.PayloadJson)
                    ?? throw new InvalidOperationException(
                        "Invalid Cost Code create payload.");

                var response = await client.PostAsJsonAsync(
                    "CostCodes",
                    request,
                    cancellationToken);

                response.EnsureSuccessStatusCode();

                break;
            }

            case OfflineSyncOperationType.Update:
            {
                if (!Guid.TryParse(
                        operation.EntityId,
                        out var costCodeId))
                {
                    throw new InvalidOperationException(
                        "Invalid Cost Code entity identifier.");
                }

                var request =
                    JsonSerializer.Deserialize<UpdateCostCodeResponse>(
                        operation.PayloadJson)
                    ?? throw new InvalidOperationException(
                        "Invalid Cost Code update payload.");

                var response = await client.PutAsJsonAsync(
                    $"CostCodes/{costCodeId}",
                    request,
                    cancellationToken);

                response.EnsureSuccessStatusCode();

                break;
            }

            case OfflineSyncOperationType.Delete:
            {
                if (!Guid.TryParse(
                        operation.EntityId,
                        out var costCodeId))
                {
                    throw new InvalidOperationException(
                        "Invalid Cost Code entity identifier.");
                }

                var response = await client.DeleteAsync(
                    $"CostCodes/{costCodeId}",
                    cancellationToken);

                response.EnsureSuccessStatusCode();

                break;
            }

            case OfflineSyncOperationType.ChangeStatus:
            {
                if (!Guid.TryParse(
                        operation.EntityId,
                        out var costCodeId))
                {
                    throw new InvalidOperationException(
                        "Invalid Cost Code entity identifier.");
                }

                var payload =
                    JsonSerializer.Deserialize<JsonElement>(
                        operation.PayloadJson);

                if (!payload.TryGetProperty(
                        "IsActive",
                        out var isActiveProperty))
                {
                    throw new InvalidOperationException(
                        "Invalid Cost Code status payload.");
                }

                var response = await client.PutAsJsonAsync(
                    $"CostCodes/{costCodeId}/active-status",
                    new
                    {
                        IsActive = isActiveProperty.GetBoolean()
                    },
                    cancellationToken);

                response.EnsureSuccessStatusCode();

                break;
            }

            default:
                throw new NotSupportedException(
                    $"Offline synchronization operation '{operation.OperationType}' is not supported for CostCode.");
        }
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
}