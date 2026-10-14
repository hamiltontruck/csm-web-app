using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using CSM.Web.Models.Procurement;

namespace CSM.Web.Services;

public class ProcurementService : IProcurementService
{
    private readonly IHttpClientFactory _factory;
    private readonly ProtectedSessionStorage _storage;

    public ProcurementService(
        IHttpClientFactory factory,
        ProtectedSessionStorage storage)
    {
        _factory = factory;
        _storage = storage;
    }

    private async Task<HttpClient?> GetClientAsync()
    {
        var token = await _storage.GetAsync<string>("jwt");

        if (!token.Success || string.IsNullOrWhiteSpace(token.Value))
            return null;

        var client = _factory.CreateClient("CSM.Api");

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token.Value);

        return client;
    }

    public async Task<List<MaterialResponse>> GetMaterialsAsync(
        bool? isActive = null,
        string? category = null)
    {
        var client = await GetClientAsync();
        if (client is null)
            return [];

        var url = "materials";

        var query = new List<string>();

        if (isActive.HasValue)
            query.Add($"isActive={isActive.Value}");

        if (!string.IsNullOrWhiteSpace(category))
            query.Add($"category={Uri.EscapeDataString(category)}");

        if (query.Count > 0)
            url += "?" + string.Join("&", query);

        var response = await client.GetAsync(url);

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<List<MaterialResponse>>() ?? [];
    }

    public async Task<MaterialResponse?> GetMaterialAsync(Guid id)
    {
        var client = await GetClientAsync();
        if (client is null) return null;

        return await client.GetFromJsonAsync<MaterialResponse>($"materials/{id}");
    }

    public async Task<bool> CreateMaterialAsync(CreateMaterialRequest request)
    {
        var client = await GetClientAsync();
        if (client is null) return false;

        var response = await client.PostAsJsonAsync("materials", request);

        return response.IsSuccessStatusCode;
    }

    public async Task<bool> UpdateMaterialAsync(
        Guid id,
        UpdateMaterialRequest request)
    {
        var client = await GetClientAsync();
        if (client is null)
            return false;

        var response = await client.PutAsJsonAsync($"materials/{id}", request);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();

            Console.WriteLine("===== UPDATE MATERIAL ERROR =====");
            Console.WriteLine(error);

            return false;
        }

        return true;
    }

   public async Task<List<MaterialLookupDto>> GetCategoriesAsync()
   {
       var client = await GetClientAsync();
       if (client is null) return [];

       return await client.GetFromJsonAsync<List<MaterialLookupDto>>(
           "materials/lookup/categories") ?? [];
   }

   public async Task<List<MaterialLookupDto>> GetUnitsAsync()
   {
       var client = await GetClientAsync();
       if (client is null) return [];

       return await client.GetFromJsonAsync<List<MaterialLookupDto>>(
           "materials/lookup/units") ?? [];
   }

   public async Task<List<MaterialLookupDto>> GetMaterialTypesAsync()
   {
       var client = await GetClientAsync();
       if (client is null) return [];

       return await client.GetFromJsonAsync<List<MaterialLookupDto>>(
           "materials/lookup/material-types") ?? [];
   }

   public async Task<List<StockItemResponse>> GetStockItemsAsync()
   {
       var client = await GetClientAsync();

       if (client is null)
           return [];

       return await client.GetFromJsonAsync<List<StockItemResponse>>(
           "stockitems") ?? [];
   }

   public async Task<List<MaterialRequestResponse>> GetMaterialRequestsAsync()
   {
       var client = await GetClientAsync();

       if (client is null)
           return [];

       var response = await client.GetAsync("materialrequests");

       if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
           return [];

       response.EnsureSuccessStatusCode();

       return await response.Content.ReadFromJsonAsync<List<MaterialRequestResponse>>() ?? [];
   }

   public async Task CreateMaterialRequestAsync(CreateMaterialRequestRequest request)
   {
       var client = await GetClientAsync();

       if (client is null)
           throw new InvalidOperationException("API client not available.");

       var response = await client.PostAsJsonAsync("materialrequests", request);

       if (!response.IsSuccessStatusCode)
       {
           var error = await response.Content.ReadAsStringAsync();
           throw new Exception(error);
       }
   }

   public async Task<MaterialRequestResponse?> GetMaterialRequestAsync(Guid id)
   {
       var client = await GetClientAsync();

       if (client is null)
           return null;

       return await client.GetFromJsonAsync<MaterialRequestResponse>(
           $"materialrequests/{id}");
   }

   public async Task<List<PurchaseOrderResponse>> GetPurchaseOrdersAsync()
   {
       var client = await GetClientAsync();

       if (client is null)
           return [];

       return await client.GetFromJsonAsync<List<PurchaseOrderResponse>>(
           "purchaseorders") ?? [];
   }

   public async Task<List<PurchaseOrderResponse>> GetReceivablePurchaseOrdersAsync()
   {
       var orders = await GetPurchaseOrdersAsync();

       return orders
           .Where(po =>
               po.Status == "SentToVendor" ||
               po.Status == "PartiallyDelivered")
           .Where(po => po.Lines.Any(line =>
               line.OrderedQuantity > line.ReceivedQuantity))
           .OrderByDescending(po => po.OrderDate)
           .ToList();
   }

   public async Task<PurchaseOrderResponse?> GetPurchaseOrderAsync(Guid id)
   {
       var client = await GetClientAsync();

       if (client is null)
           return null;

       return await client.GetFromJsonAsync<PurchaseOrderResponse>(
           $"purchaseorders/{id}");
   }

  public async Task CreatePurchaseOrderAsync(CreatePurchaseOrderRequest request)
  {
      var client = await GetClientAsync();

      if (client is null)
          throw new InvalidOperationException("API client not available.");

      // PRINT THE REQUEST
      var json = System.Text.Json.JsonSerializer.Serialize(
          request,
          new System.Text.Json.JsonSerializerOptions
          {
              WriteIndented = true
          });

      Console.WriteLine("===== PURCHASE ORDER REQUEST =====");
      Console.WriteLine(json);

      var response = await client.PostAsJsonAsync("purchaseorders", request);

      if (!response.IsSuccessStatusCode)
      {
          var error = await response.Content.ReadAsStringAsync();

          Console.WriteLine("===== PURCHASE ORDER ERROR =====");
          Console.WriteLine(error);

          throw new Exception(error);
      }
  }

   public async Task<List<SupplierResponse>> GetVendorsAsync()
   {
       var client = await GetClientAsync();
       if (client is null)
           return [];

       var response = await client.GetAsync("vendors");

       var json = await response.Content.ReadAsStringAsync();

       Console.WriteLine("===== VENDORS JSON =====");
       Console.WriteLine(json);

       response.EnsureSuccessStatusCode();

       return System.Text.Json.JsonSerializer.Deserialize<List<SupplierResponse>>(
           json,
           new System.Text.Json.JsonSerializerOptions
           {
               PropertyNameCaseInsensitive = true
           }) ?? [];
   }


   public async Task<List<GoodsReceiptResponse>> GetGoodsReceiptsAsync()
   {
       var client = await GetClientAsync();
       if (client is null) return [];

       return await client.GetFromJsonAsync<List<GoodsReceiptResponse>>(
           "goodsreceipts") ?? [];
   }

   public async Task<GoodsReceiptResponse?> GetGoodsReceiptAsync(Guid id)
   {
       var client = await GetClientAsync();
       if (client is null) return null;

       return await client.GetFromJsonAsync<GoodsReceiptResponse>(
           $"goodsreceipts/{id}");
   }

   public async Task CreateGoodsReceiptAsync(CreateGoodsReceiptRequest request)
   {
       var client = await GetClientAsync();

       if (client is null)
           return;

       Console.WriteLine("===== GOODS RECEIPT REQUEST =====");
       Console.WriteLine(
           System.Text.Json.JsonSerializer.Serialize(
               request,
               new System.Text.Json.JsonSerializerOptions
               {
                   WriteIndented = true
               }));

       var response = await client.PostAsJsonAsync("goodsreceipts", request);

       if (!response.IsSuccessStatusCode)
       {
           var error = await response.Content.ReadAsStringAsync();

           Console.WriteLine("===== GOODS RECEIPT ERROR =====");
           Console.WriteLine(error);

           throw new Exception(error);
       }
   }

   public async Task SendPurchaseOrderToVendorAsync(Guid purchaseOrderId)
   {
       var client = await GetClientAsync();

       if (client is null)
           return;

       var response = await client.PostAsync(
           $"purchaseorders/{purchaseOrderId}/send-to-vendor",
           null);

       if (!response.IsSuccessStatusCode)
       {
           var error = await response.Content.ReadAsStringAsync();

           Console.WriteLine("===== SEND TO VENDOR ERROR =====");
           Console.WriteLine(error);

           throw new Exception(error);
       }
   }

   public async Task ApprovePurchaseOrderAsync(Guid purchaseOrderId)
   {
       var client = await GetClientAsync();

       if (client is null)
           return;

       var response = await client.PostAsync(
           $"purchaseorders/{purchaseOrderId}/approve",
           null);

       if (!response.IsSuccessStatusCode)
       {
           var error = await response.Content.ReadAsStringAsync();
           throw new Exception(error);
       }
   }

   public async Task SubmitPurchaseOrderForApprovalAsync(Guid purchaseOrderId)
   {
       var client = await GetClientAsync();

       if (client is null)
           return;

       var response = await client.PostAsync(
           $"purchaseorders/{purchaseOrderId}/submit",
           null);

       if (!response.IsSuccessStatusCode)
       {
           var error = await response.Content.ReadAsStringAsync();
           throw new Exception(error);
       }
   }

}