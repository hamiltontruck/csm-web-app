using System.Net;

namespace CSM.Web.Services;

// Created only for allowlisted domain validation messages from a 400 response.
internal sealed class EquipmentDowntimeValidationException : HttpRequestException
{
    public EquipmentDowntimeValidationException(string message)
        : base(message, null, HttpStatusCode.BadRequest)
    {
    }
}
