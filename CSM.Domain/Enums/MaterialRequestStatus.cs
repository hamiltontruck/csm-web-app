namespace CSM.Domain.Enums;

public enum MaterialRequestStatus
{
    Draft = 1,
    Submitted = 2,
    Approved = 3,
    PartiallyApproved = 4,
    Rejected = 5,
    Ordered = 6,
    PartiallyDelivered = 7,
    Delivered = 8,
    Cancelled = 9
}