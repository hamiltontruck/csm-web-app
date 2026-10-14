namespace CSM.Application.Auth.Dtos;

public sealed record LoginRequest(
    string Email,
    string Password,
    string? DeviceName);