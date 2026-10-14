namespace CSM.Web.Models.Procurement;

public class CreateStockReturnRequest
{
    public Guid OriginalIssueMovementId { get; set; }

    public decimal Quantity { get; set; }

    public DateTime ReturnDateUtc { get; set; }

    public string ReferenceNumber { get; set; } = "";

    public string? ReturnedBy { get; set; }

    public string? Reason { get; set; }

    public string? Remarks { get; set; }
}