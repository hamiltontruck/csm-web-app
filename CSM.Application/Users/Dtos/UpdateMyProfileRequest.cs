namespace CSM.Application.Users.Dtos;

public sealed record UpdateMyProfileRequest(
    string FirstName,
    string LastName,
    string PhoneNumber,
    string JobTitle
);