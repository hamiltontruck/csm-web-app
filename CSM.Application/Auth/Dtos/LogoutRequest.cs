namespace CSM.Application.Auth.Dtos;

public sealed record LogoutRequest(
    string RefreshToken);