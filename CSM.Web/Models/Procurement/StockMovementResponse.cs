using System.Text.Json.Serialization;

namespace CSM.Web.Models.Procurement;

public class StockMovementResponse
{
    public Guid Id { get; set; }

    public Guid StockItemId { get; set; }

    public Guid ConstructionSiteId { get; set; }
    public string SiteName { get; set; } = "";

    public Guid MaterialId { get; set; }
    public string MaterialCode { get; set; } = "";
    public string MaterialName { get; set; } = "";

    public string UnitOfMeasure { get; set; } = "";

    public string MovementType { get; set; } = "";

    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public decimal Quantity { get; set; }

    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public decimal? StockBalanceBefore { get; set; }

    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public decimal? StockBalanceAfter { get; set; }

    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public decimal? UnitCost { get; set; }

    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public decimal? TotalCost { get; set; }

    public DateTime MovementDateUtc { get; set; }

    public string? ReferenceType { get; set; }
    public Guid? ReferenceId { get; set; }

    public string? ReferenceNumber { get; set; }
    public string? Remarks { get; set; }

    public string? MoveFrom { get; set; }
    public string? MoveTo { get; set; }
}