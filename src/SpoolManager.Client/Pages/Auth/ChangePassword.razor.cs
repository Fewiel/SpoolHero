using Microsoft.AspNetCore.Components;
using System.Net.Http.Json;
using SpoolManager.Client.Services;

namespace SpoolManager.Client.Pages.Auth;

public partial class ChangePassword
{
    [Inject] private AuthService Auth { get; set; } = default!;
    [Inject] private LocalizationService L { get; set; } = default!;
    [Inject] private NavigationManager Nav { get; set; } = default!;
    [Inject] private System.Net.Http.HttpClient Http { get; set; } = default!;

    private ChangePasswordModel _model = new();
    private string _confirmPassword = string.Empty;
    private string? _message;
    private bool _success;
    private bool _loading;

    private class ChangePasswordModel
    {
        public string CurrentPassword { get; set; } = string.Empty;
        public string NewPassword { get; set; } = string.Empty;
    }

    private async Task SubmitAsync()
    {
        _message = null;

        if (_model.NewPassword != _confirmPassword)
        {
            _message = L["auth.password.mismatch"];
            _success = false;
            return;
        }

        _loading = true;
        try
        {
            var response = await Http.PutAsJsonAsync("api/auth/password", new
            {
                CurrentPassword = _model.CurrentPassword,
                NewPassword = _model.NewPassword
            });

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<PasswordChangeResult>();
                if (result?.AccessToken != null)
                {
                    await Auth.UpdateTokenAsync(result.AccessToken);
                }
                _message = L["auth.password.changed"];
                _success = true;
                _model = new();
                _confirmPassword = string.Empty;
            }
            else
            {
                var error = await response.Content.ReadFromJsonAsync<ErrorResult>();
                _message = error?.Message ?? L["auth.password.error"];
                _success = false;
            }
        }
        catch
        {
            _message = L["auth.password.error"];
            _success = false;
        }
        finally
        {
            _loading = false;
        }
    }

    private record PasswordChangeResult(string? AccessToken, string? Message);
    private record ErrorResult(string? Message);
}
