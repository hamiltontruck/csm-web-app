using CSM.Domain.Enums;

namespace CSM.Application.Finance.Invoices.Dtos;

public sealed record ChangeInvoiceStatusRequest(
    InvoiceStatus Status);
