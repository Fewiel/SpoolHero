using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Components;
using SpoolManager.Client.Services;
using SpoolManager.Shared.DTOs.Auth;

namespace SpoolManager.Client.Pages.Auth;

public partial class Register
{
    [Inject] private System.Net.Http.HttpClient Http { get; set; } = default!;
    [Inject] private LocalizationService L { get; set; } = default!;
    [Inject] private NavigationManager Nav { get; set; } = default!;
    [Inject] private AuthService Auth { get; set; } = default!;

    private readonly RegisterRequest _model = new();
    private readonly Dictionary<string, string> _errors = [];
    private string _confirmPassword = string.Empty;
    private bool _loading;
    private bool _success;
    private bool _requiresVerification;
    private string? _error;
    private bool _termsAccepted;
    private bool _privacyAccepted;

    protected override void OnInitialized()
    {
        if (Auth.IsAuthenticated)
            Nav.NavigateTo("/");
    }

    private string Invalid(string field) => _errors.ContainsKey(field) ? "is-invalid" : string.Empty;

    private bool Validate()
    {
        _errors.Clear();
        _model.Username = _model.Username?.Trim() ?? string.Empty;
        _model.Email = _model.Email?.Trim() ?? string.Empty;

        if (_model.Username.Length < 3)
            _errors["username"] = L["auth.username.min"];
        if (!LoginForm.IsEmail(_model.Email))
            _errors["email"] = L["auth.email.invalid"];
        if ((_model.Password ?? string.Empty).Length < 8)
            _errors["password"] = L["auth.password.min"];
        else if (_model.Password != _confirmPassword)
            _errors["confirm"] = L["auth.password.mismatch"];
        if (!_termsAccepted || !_privacyAccepted)
            _errors["consent"] = L["auth.terms.required"];

        return _errors.Count == 0;
    }

    private async Task HandleRegister()
    {
        _error = null;
        if (!Validate())
            return;

        _loading = true;
        try
        {
            var resp = await Http.PostAsJsonAsync("api/auth/register", _model);
            if (resp.IsSuccessStatusCode)
            {
                var json = await resp.Content.ReadFromJsonAsync<RegisterResponse>();
                if (json?.RequiresEmailVerification == true)
                {
                    _requiresVerification = true;
                }
                else
                {
                    _success = true;
                    _loading = false;
                    StateHasChanged();
                    await Task.Delay(2500);
                    Nav.NavigateTo("/login");
                }
            }
            else
            {
                _error = resp.StatusCode switch
                {
                    HttpStatusCode.Conflict => L["auth.register.taken"],
                    HttpStatusCode.TooManyRequests => L["auth.login.blocked"],
                    HttpStatusCode.BadRequest => L["auth.register.invalid"],
                    _ => L["auth.register.failed"]
                };
            }
        }
        catch
        {
            _error = L["auth.register.failed"];
        }
        _loading = false;
    }

    private record RegisterResponse(bool RequiresEmailVerification);
}
