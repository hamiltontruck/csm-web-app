using CSM.Web.Services;
using CSM.Web.Services.Access;
using CSM.Web.Models.Security;
using CSM.Web.Models.UserPreferences;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
namespace CSM.Web.Components.Pages;
public partial class Settings
{
private bool _showPasswordChange;
private bool _passwordChangeSubmitting;
private string _currentPassword = string.Empty;
private string _newPassword = string.Empty;
private string _confirmNewPassword = string.Empty;
private string? _passwordChangeError;
private string? _passwordChangeSuccess;

private void OpenPasswordChange()
{
    _passwordChangeError = null;
    _passwordChangeSuccess = null;
    _showPasswordChange = true;
}

private void ClosePasswordChange()
{
    _showPasswordChange = false;
    _currentPassword = string.Empty;
    _newPassword = string.Empty;
    _confirmNewPassword = string.Empty;
    _passwordChangeError = null;
    _passwordChangeSuccess = null;
}

private async Task SubmitPasswordChangeAsync()
{
    if (_passwordChangeSubmitting)
    {
        return;
    }

    _passwordChangeError = null;
    _passwordChangeSuccess = null;

    if (string.IsNullOrWhiteSpace(_currentPassword) ||
        string.IsNullOrWhiteSpace(_newPassword) ||
        string.IsNullOrWhiteSpace(_confirmNewPassword))
    {
        _passwordChangeError =
            "Complete all three password fields.";
        return;
    }

    if (_newPassword != _confirmNewPassword)
    {
        _passwordChangeError =
            "The new password and confirmation do not match.";
        return;
    }

    if (_newPassword.Length < 12 || !_newPassword.Any(char.IsUpper) || !_newPassword.Any(char.IsLower) ||
        !_newPassword.Any(char.IsDigit) || !_newPassword.Any(ch => !char.IsLetterOrDigit(ch)))
    {
        _passwordChangeError =
            "Use at least 12 characters with uppercase, lowercase, a digit and a symbol.";
        return;
    }

    if (_currentPassword == _newPassword)
    {
        _passwordChangeError =
            "The new password must differ from the current password.";
        return;
    }

    _passwordChangeSubmitting = true;

    try
    {
        await PasswordChanges.ChangePasswordAsync(
            _currentPassword,
            _newPassword,
            _confirmNewPassword);

        _currentPassword = string.Empty;
        _newPassword = string.Empty;
        _confirmNewPassword = string.Empty;

        await SignOutAsync();
    }
    catch (HttpRequestException)
    {
        _passwordChangeError =
            "Password change failed. Check your current password and password requirements, then try again.";
    }
    catch (OperationCanceledException)
    {
        _passwordChangeError = "Password change could not be confirmed. Try signing in with your new password before retrying.";
    }
    catch (Exception ex)
    {
        Logger.LogWarning(ex, "Password change response could not be processed.");
        _passwordChangeError = "Password change could not be confirmed. Please try signing in again.";
    }
    finally
    {
        _passwordChangeSubmitting = false;
    }
}

private async Task LoadCurrentUserProfileAsync()
{
    _currentUserProfileLoading = true;
    _currentUserProfileError = null;

    try
    {
        _currentUserProfile =
            await CurrentUserProfileService.GetAsync();

        _currentUserProfileLoaded = true;
    }
    catch (HttpRequestException)
    {
        _currentUserProfileError =
            "Unable to load your account information. Please try again.";
    }
    catch (Exception ex)
    {
        Logger.LogError(ex, "Failed to load the current user's profile summary.");
        _currentUserProfileError =
            "An unexpected error occurred while loading your account information.";
    }
    finally
    {
        _currentUserProfileLoading = false;
    }
}

private sealed class MyProfileFormModel
{
    [System.ComponentModel.DataAnnotations.Required]
    [System.ComponentModel.DataAnnotations.StringLength(100)]
    public string FirstName { get; set; } = string.Empty;

    [System.ComponentModel.DataAnnotations.Required]
    [System.ComponentModel.DataAnnotations.StringLength(100)]
    public string LastName { get; set; } = string.Empty;

    [System.ComponentModel.DataAnnotations.StringLength(30)]
    public string PhoneNumber { get; set; } = string.Empty;

    [System.ComponentModel.DataAnnotations.StringLength(100)]
    public string JobTitle { get; set; } = string.Empty;
}

private MyProfileFormModel _myProfileForm = new();
private bool _myProfileLoading;
private bool _myProfileLoaded;
private bool _myProfileSaving;
private string? _myProfileError;
private string? _myProfileSuccess;

private async Task LoadOrganizationMembershipAsync()
{
    _organizationMembershipLoading = true;
    _organizationMembershipError = null;

    try
    {
        var memberships =
            await OrganizationMembership.GetAsync();

        _organizationMemberships = memberships.ToList();
        _organizationMembershipLoaded = true;
    }
    catch (HttpRequestException)
    {
        _organizationMembershipError =
            "Unable to load organization membership. Please try again.";
    }
    catch (Exception ex)
    {
        Logger.LogError(ex, "Failed to load organization membership.");
        _organizationMembershipError =
            "An unexpected error occurred while loading organization membership.";
    }
    finally
    {
        _organizationMembershipLoading = false;
    }
}

private async Task LoadMyProfileForEditAsync()
{
    if (_myProfileSaving || _myProfileLoading) return;
    _myProfileLoading = true;
    _myProfileError = null;
    _myProfileSuccess = null;

    try
    {
        var profile = await MyProfileService.GetAsync();

        _myProfileForm = new MyProfileFormModel
        {
            FirstName = profile.FirstName,
            LastName = profile.LastName,
            PhoneNumber = profile.PhoneNumber,
            JobTitle = profile.JobTitle
        };

        _myProfileLoaded = true;
    }
    catch (HttpRequestException ex)
    {
        _myProfileError = ex.StatusCode switch
        {
            System.Net.HttpStatusCode.Unauthorized =>
                "Your session has expired. Please sign in again.",
            System.Net.HttpStatusCode.NotFound =>
                "Your account could not be found.",
            _ => "Unable to load your profile. Please try again."
        };
    }
    catch (Exception ex)
    {
        Logger.LogError(ex, "Failed to load profile details for editing.");
        _myProfileError =
            "An unexpected error occurred while loading your profile.";
    }
    finally
    {
        _myProfileLoading = false;
    }
}

private async Task SaveMyProfileAsync()
{
    if (_myProfileSaving)
    {
        return;
    }

    _myProfileSaving = true;
    _myProfileError = null;
    _myProfileSuccess = null;

    try
    {
        var request = new MyProfileData
        {
            FirstName = _myProfileForm.FirstName.Trim(),
            LastName = _myProfileForm.LastName.Trim(),
            PhoneNumber = _myProfileForm.PhoneNumber.Trim(),
            JobTitle = _myProfileForm.JobTitle.Trim()
        };

        var updated = await MyProfileService.UpdateAsync(request);

        _myProfileForm.FirstName = updated.FirstName;
        _myProfileForm.LastName = updated.LastName;
        _myProfileForm.PhoneNumber = updated.PhoneNumber;
        _myProfileForm.JobTitle = updated.JobTitle;

        _myProfileSuccess = "Profile updated successfully.";
        _currentUserProfileLoaded = false;
    }
    catch (HttpRequestException ex)
    {
        _myProfileError = ex.StatusCode switch
        {
            System.Net.HttpStatusCode.Unauthorized =>
                "Your session has expired. Please sign in again.",
            System.Net.HttpStatusCode.BadRequest =>
                "The profile details are invalid. Check the fields and try again.",
            System.Net.HttpStatusCode.NotFound =>
                "Your account could not be found.",
            _ => "Unable to save your profile. Please try again."
        };
    }
    catch (Exception ex)
    {
        Logger.LogError(ex, "Failed to save the current user's profile.");
        _myProfileError =
            "An unexpected error occurred while saving your profile.";
    }
    finally
    {
        _myProfileSaving = false;
    }
}

private void CancelMyProfileEdit()
{
    if (_myProfileSaving) return;
    _myProfileLoaded = false;
    _myProfileError = null;
    _myProfileSuccess = null;
}


    private async Task SignOutAsync()
    {
        await AuthService.LogoutAsync();
        Navigation.NavigateTo("/login", forceLoad: true);
    }

}
