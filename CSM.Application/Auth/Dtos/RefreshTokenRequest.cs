namespace CSM.Application.Auth.Dtos;

public sealed record RefreshTokenRequest(
    string RefreshToken,
    string? DeviceName);