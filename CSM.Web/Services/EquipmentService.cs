using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using CSM.Web.Models.Equipment;

namespace CSM.Web.Services;

public class EquipmentService : IEquipmentService
{
    private readonly IHttpClientFactory _factory;
    private readonly IAuthService _auth;

    public EquipmentService(
        IHttpClientFactory factory,
        IAuthService auth)
    {
        _factory = factory;
        _auth = auth;
    }

    private async Task<HttpClient> GetClientAsync()
    {
        var client = _factory.CreateClient("CSM.Api");

        await _auth.ApplyAuthenticationAsync(client);

        return client;
    }

    public async Task<List<EquipmentResponse>> GetEquipmentAsync()
    {
        var client = await GetClientAsync();

        var response = await client.GetAsync("equipment");

        var json = await response.Content.ReadAsStringAsync();

        Console.WriteLine("===== EQUIPMENT LIST JSON =====");
        Console.WriteLine(json);

        response.EnsureSuccessStatusCode();

        return System.Text.Json.JsonSerializer.Deserialize<List<EquipmentResponse>>(
            json,
            new System.Text.Json.JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? [];
    }

    public async Task<EquipmentResponse?> GetEquipmentByIdAsync(Guid id)
    {
        var client = await GetClientAsync();

        return await client.GetFromJsonAsync<EquipmentResponse>(
            $"equipment/{id}");
    }

    public async Task<Guid> CreateEquipmentAsync(CreateEquipmentRequest request)
    {
        var client = await GetClientAsync();

        Console.WriteLine("===== CREATE EQUIPMENT REQUEST =====");
        Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(
            request,
            new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true
            }));

        var response = await client.PostAsJsonAsync("equipment", request);

        var body = await response.Content.ReadAsStringAsync();

        Console.WriteLine("===== CREATE EQUIPMENT RESPONSE =====");
        Console.WriteLine(body);

        if (!response.IsSuccessStatusCode)
            throw new Exception(body);

        var created = System.Text.Json.JsonSerializer.Deserialize<EquipmentResponse>(
            body,
            new System.Text.Json.JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

        return created!.Id;
    }

    public async Task UpdateEquipmentAsync(Guid id, UpdateEquipmentRequest request)
    {
        var client = await GetClientAsync();

        Console.WriteLine("===== UPDATE REQUEST =====");
        Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(
            request,
            new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true
            }));

        var response = await client.PutAsJsonAsync(
            $"equipment/{id}",
            request);

        var body = await response.Content.ReadAsStringAsync();

        Console.WriteLine("===== UPDATE RESPONSE =====");
        Console.WriteLine(body);

        if (!response.IsSuccessStatusCode)
            throw new Exception(body);
    }

    public async Task<List<EquipmentAssignmentResponse>> GetAssignmentsAsync()
    {
        var client = await GetClientAsync();

        return await client.GetFromJsonAsync<List<EquipmentAssignmentResponse>>(
                   "equipmentassignments")
               ?? [];
    }

    public async Task<Guid> CreateAssignmentAsync(
        CreateEquipmentAssignmentRequest request)
    {
        var client = await GetClientAsync();

        Console.WriteLine("===== CREATE ASSIGNMENT REQUEST =====");
        Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(
            request,
            new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true
            }));

        var response = await client.PostAsJsonAsync(
            "equipmentassignments",
            request);

        var body = await response.Content.ReadAsStringAsync();

        Console.WriteLine("===== CREATE ASSIGNMENT RESPONSE =====");
        Console.WriteLine(body);

        if (!response.IsSuccessStatusCode)
            throw new Exception(body);

        var created =
            System.Text.Json.JsonSerializer.Deserialize<EquipmentAssignmentResponse>(
                body,
                new System.Text.Json.JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

        return created!.Id;
    }

    public async Task<EquipmentAssignmentResponse> ReleaseAssignmentAsync(
        Guid assignmentId,
        ReleaseEquipmentAssignmentRequest request)
    {
        var client = await GetClientAsync();

        var response = await client.PutAsJsonAsync(
            $"equipmentassignments/{assignmentId}/release",
            request);

        var body = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            throw new Exception(body);

        return System.Text.Json.JsonSerializer.Deserialize<EquipmentAssignmentResponse>(
            body,
            new System.Text.Json.JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            })!;
    }

    public async Task<List<EquipmentDowntimeResponse>> GetDowntimeAsync(
        Guid? equipmentId = null,
        Guid? constructionSiteId = null,
        bool? openOnly = null)
    {
        var client = await GetClientAsync();

        var query = new List<string>();

        if (equipmentId.HasValue)
            query.Add($"equipmentId={equipmentId.Value}");

        if (constructionSiteId.HasValue)
            query.Add($"constructionSiteId={constructionSiteId.Value}");

        if (openOnly.HasValue)
            query.Add($"openOnly={openOnly.Value.ToString().ToLowerInvariant()}");

        var url = "equipmentdowntime";

        if (query.Count > 0)
            url += "?" + string.Join("&", query);

        return await client.GetFromJsonAsync<List<EquipmentDowntimeResponse>>(url)
               ?? [];
    }

    public async Task<Guid> CreateDowntimeAsync(
        CreateEquipmentDowntimeRequest request)
    {
        var client = await GetClientAsync();

        Console.WriteLine("===== CREATE EQUIPMENT DOWNTIME REQUEST =====");
        Console.WriteLine(
            System.Text.Json.JsonSerializer.Serialize(
                request,
                new System.Text.Json.JsonSerializerOptions
                {
                    WriteIndented = true
                }));

        var response = await client.PostAsJsonAsync(
            "equipmentdowntime",
            request);

        var body = await response.Content.ReadAsStringAsync();

        Console.WriteLine("===== CREATE EQUIPMENT DOWNTIME RESPONSE =====");
        Console.WriteLine(body);

        if (!response.IsSuccessStatusCode)
            throw new Exception(body);

        var created =
            System.Text.Json.JsonSerializer.Deserialize<EquipmentDowntimeResponse>(
                body,
                new System.Text.Json.JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

        return created!.Id;
    }

    public async Task<EquipmentDowntimeResponse> UpdateDowntimeAsync(
        Guid downtimeId,
        UpdateEquipmentDowntimeRequest request)
    {
        var client = await GetClientAsync();

        var response = await client.PutAsJsonAsync(
            $"equipmentdowntime/{downtimeId}",
            request);

        var body = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            throw new Exception(body);

        return System.Text.Json.JsonSerializer.Deserialize<EquipmentDowntimeResponse>(
            body,
            new System.Text.Json.JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            })!;
    }

    public async Task<EquipmentDowntimeResponse> CloseDowntimeAsync(
        Guid downtimeId,
        CloseEquipmentDowntimeRequest request)
    {
        var client = await GetClientAsync();

        var response = await client.PutAsJsonAsync(
            $"equipmentdowntime/{downtimeId}/close",
            request);

        var body = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            throw new Exception(body);

        return System.Text.Json.JsonSerializer.Deserialize<EquipmentDowntimeResponse>(
            body,
            new System.Text.Json.JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            })!;
    }

        public async Task<List<EquipmentUtilizationResponse>> GetUtilizationAsync()
        {
            var client = await GetClientAsync();

            var response = await client.GetAsync(
                "equipmentutilization");

            var body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                throw new Exception(body);

            return System.Text.Json.JsonSerializer.Deserialize<
                List<EquipmentUtilizationResponse>>(
                    body,
                    new System.Text.Json.JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    }) ?? [];
        }

        public async Task<List<EquipmentMaintenanceResponse>> GetMaintenanceAsync(
            Guid? equipmentId = null,
            bool? completedOnly = null)
        {
            var client = await GetClientAsync();

            var query = new List<string>();

            if (equipmentId.HasValue)
                query.Add($"equipmentId={equipmentId.Value}");

            if (completedOnly.HasValue)
                query.Add($"completedOnly={completedOnly.Value.ToString().ToLowerInvariant()}");

            var url = "equipmentmaintenance";

            if (query.Count > 0)
                url += "?" + string.Join("&", query);

            return await client.GetFromJsonAsync<List<EquipmentMaintenanceResponse>>(url)
                   ?? [];
        }

        public async Task<EquipmentMaintenanceResponse?> GetMaintenanceByIdAsync(
            Guid maintenanceId)
        {
            var client = await GetClientAsync();

            return await client.GetFromJsonAsync<EquipmentMaintenanceResponse>(
                $"equipmentmaintenance/{maintenanceId}");
        }

        public async Task<Guid> CreateMaintenanceAsync(
            CreateEquipmentMaintenanceRequest request)
        {
            var client = await GetClientAsync();

            var response = await client.PostAsJsonAsync(
                "equipmentmaintenance",
                request);

            var body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                throw new Exception(body);

            var created =
                System.Text.Json.JsonSerializer.Deserialize<EquipmentMaintenanceResponse>(
                    body,
                    new System.Text.Json.JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

            return created!.Id;
        }

        public async Task<EquipmentMaintenanceResponse> UpdateMaintenanceAsync(
            Guid maintenanceId,
            UpdateEquipmentMaintenanceRequest request)
        {
            var client = await GetClientAsync();

            var response = await client.PutAsJsonAsync(
                $"equipmentmaintenance/{maintenanceId}",
                request);

            var body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                throw new Exception(body);

            return System.Text.Json.JsonSerializer.Deserialize<EquipmentMaintenanceResponse>(
                body,
                new System.Text.Json.JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                })!;
        }

        public async Task<List<EquipmentFuelLogResponse>> GetFuelLogsAsync(
            Guid? equipmentId = null)
        {
            var client = await GetClientAsync();

            var url = "equipmentfuellogs";

            if (equipmentId.HasValue)
                url += $"?equipmentId={equipmentId.Value}";

            var response = await client.GetAsync(url);

            var body = await response.Content.ReadAsStringAsync();

            response.EnsureSuccessStatusCode();

            return System.Text.Json.JsonSerializer.Deserialize<
                List<EquipmentFuelLogResponse>>(
                    body,
                    new System.Text.Json.JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    }) ?? [];
        }

        public async Task<Guid> CreateFuelLogAsync(
            CreateEquipmentFuelLogRequest request)
        {
            var client = await GetClientAsync();

            var response = await client.PostAsJsonAsync(
                "equipmentfuellogs",
                request);

            var body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                throw new Exception(body);

            var created =
                System.Text.Json.JsonSerializer.Deserialize<EquipmentFuelLogResponse>(
                    body,
                    new System.Text.Json.JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

            return created!.Id;
        }

        public async Task<EquipmentFuelLogResponse?> GetFuelLogByIdAsync(Guid fuelLogId)
        {
            var client = await GetClientAsync();

            return await client.GetFromJsonAsync<EquipmentFuelLogResponse>(
                $"equipmentfuellogs/{fuelLogId}");
        }

        public async Task UpdateFuelLogAsync(
            Guid fuelLogId,
            UpdateEquipmentFuelLogRequest request)
        {
            var client = await GetClientAsync();

            var response = await client.PutAsJsonAsync(
                $"equipmentfuellogs/{fuelLogId}",
                request);

            var body = await response.Content.ReadAsStringAsync();

            response.EnsureSuccessStatusCode();
        }

        public async Task<List<EquipmentHourLogResponse>> GetHourLogsAsync(
            Guid? equipmentId = null)
        {
            var client = await GetClientAsync();

            var url = "equipmenthourlogs";

            if (equipmentId.HasValue)
                url += $"?equipmentId={equipmentId.Value}";

            var response = await client.GetAsync(url);

            var body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                throw new Exception(body);

            return System.Text.Json.JsonSerializer.Deserialize<
                List<EquipmentHourLogResponse>>(
                    body,
                    new System.Text.Json.JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    }) ?? [];
        }

        public async Task<Guid> CreateHourLogAsync(
            CreateEquipmentHourLogRequest request)
        {
            var client = await GetClientAsync();

            var response = await client.PostAsJsonAsync(
                "equipmenthourlogs",
                request);

            var body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                throw new Exception(body);

            var created =
                System.Text.Json.JsonSerializer.Deserialize<EquipmentHourLogResponse>(
                    body,
                    new System.Text.Json.JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

            return created!.Id;
        }

        public async Task<List<EquipmentTimesheetResponse>> GetTimesheetsAsync(
            Guid? equipmentId = null)
        {
            var client = await GetClientAsync();

            var url = "equipmenttimesheets";

            if (equipmentId.HasValue)
                url += $"?equipmentId={equipmentId.Value}";

            var response = await client.GetAsync(url);

            var body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                throw new Exception(body);

            return System.Text.Json.JsonSerializer.Deserialize<
                List<EquipmentTimesheetResponse>>(
                    body,
                    new System.Text.Json.JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    }) ?? [];
        }

        public async Task<Guid> CreateTimesheetAsync(
            CreateEquipmentTimesheetRequest request)
        {
            var client = await GetClientAsync();

            var response = await client.PostAsJsonAsync(
                "equipmenttimesheets",
                request);

            var body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                throw new Exception(body);

            var created =
                System.Text.Json.JsonSerializer.Deserialize<EquipmentTimesheetResponse>(
                    body,
                    new System.Text.Json.JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

            return created!.Id;
        }

        public async Task<EquipmentTimesheetResponse> GetTimesheetByIdAsync(Guid id)
        {
            var client = await GetClientAsync();

            var response = await client.GetAsync($"equipmenttimesheets/{id}");

            var body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                throw new Exception(body);

            return JsonSerializer.Deserialize<EquipmentTimesheetResponse>(
                body,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                })!;
        }

        public async Task UpdateTimesheetAsync(
            Guid id,
            CreateEquipmentTimesheetRequest request)
        {
            var client = await GetClientAsync();

            var response = await client.PutAsJsonAsync(
                $"equipmenttimesheets/{id}",
                request);

            var body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                throw new Exception(body);
        }
}