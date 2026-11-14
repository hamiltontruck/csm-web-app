namespace CSM.Web.Services;

public sealed class PurchaseOrderValidationException(string message) : Exception(message);
