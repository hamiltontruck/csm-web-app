namespace CSM.Domain.Enums;

public enum PurchaseOrderStatus
{
    Draft = 1,
    PendingApproval = 2,
    Approved = 3,
    SentToVendor = 4,
    PartiallyDelivered = 5,
    Delivered = 6,
    Closed = 7,
    Cancelled = 8
}