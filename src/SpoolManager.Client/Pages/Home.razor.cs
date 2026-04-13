using Microsoft.AspNetCore.Components;
using SpoolManager.Client.Services;
using SpoolManager.Shared.DTOs.Spools;

namespace SpoolManager.Client.Pages;

public partial class Home : IDisposable
{
    [Inject] private SpoolService Spools { get; set; } = default!;
    [Inject] private PrinterService Printers { get; set; } = default!;
    [Inject] private StorageService Storage { get; set; } = default!;
    [Inject] private LocalizationService L { get; set; } = default!;
    [Inject] private AuthService Auth { get; set; } = default!;
    [Inject] private NavigationManager Nav { get; set; } = default!;
    [Inject] private ProjectService Project { get; set; } = default!;

    private bool _loading = true;
    private bool _showOnboarding;
    private bool _onboardingSaving;
    private int _printersCount;
    private int _storageCount;
    private int _total, _active, _lowStock, _consumed;
    private int _storedCount;
    private List<SpoolDto> _recent = [];
    private List<LocationGroup> _locationGroups = [];

    private record LocationGroup(string Name, string Icon, string Color, int SpoolCount);

    protected override void OnInitialized()
    {
        Project.OnProjectChanged += OnProjectChanged;
    }

    protected override async Task OnInitializedAsync()
    {
        if (!Auth.IsAuthenticated)
        {
            Nav.NavigateTo("/login", replace: true);
            return;
        }
        _showOnboarding = Auth.CurrentUser?.DashboardOnboardingDismissed != true;
        await LoadDataAsync();
    }

    private void OnProjectChanged()
    {
        _ = InvokeAsync(LoadDataAsync);
    }

    private async Task LoadDataAsync()
    {
        if (Project.CurrentProject == null)
            return;
        _loading = true;
        StateHasChanged();

        var printers = await Printers.GetAllAsync() ?? [];
        _printersCount = printers.Count;

        var storageLocations = await Storage.GetAllAsync() ?? [];
        _storageCount = storageLocations.Count;

        var all = await Spools.GetAllAsync() ?? [];
        _total = all.Count;
        _active = all.Count(s => s.ConsumedAt == null);
        _consumed = all.Count(s => s.ConsumedAt != null);
        _lowStock = all.Count(s => s.ConsumedAt == null && s.RemainingPercent < 20);
        _recent = [.. all.OrderByDescending(s => s.CreatedAt).Take(6)];

        var activeSpools = all.Where(s => s.ConsumedAt == null).ToList();
        _storedCount = activeSpools.Count(s => s.StorageLocationName != null);
        var groups = new List<LocationGroup>();

        foreach (var g in activeSpools.Where(s => s.PrinterName != null).GroupBy(s => s.PrinterName!))
            groups.Add(new LocationGroup(g.Key, "bi-printer", "primary", g.Count()));

        foreach (var g in activeSpools.Where(s => s.StorageLocationName != null && s.PrinterName == null).GroupBy(s => s.StorageLocationName!))
            groups.Add(new LocationGroup(g.Key, "bi-box", "secondary", g.Count()));

        foreach (var g in activeSpools.Where(s => s.DryerName != null && s.PrinterName == null && s.StorageLocationName == null).GroupBy(s => s.DryerName!))
            groups.Add(new LocationGroup(g.Key, "bi-thermometer-half", "warning", g.Count()));

        var noLocation = activeSpools.Count(s => s.PrinterName == null && s.StorageLocationName == null && s.DryerName == null);
        if (noLocation > 0)
            groups.Add(new LocationGroup(L["dashboard.no.location"], "bi-question-circle", "muted", noLocation));

        _locationGroups = groups;

        if (_showOnboarding && !_onboardingSaving && IsOnboardingCompleted())
        {
            _onboardingSaving = true;
            var saved = await Auth.SetDashboardOnboardingDismissedAsync(true);
            _onboardingSaving = false;
            if (saved)
                _showOnboarding = false;
        }

        _loading = false;
        StateHasChanged();
    }

    public void Dispose()
    {
        Project.OnProjectChanged -= OnProjectChanged;
    }

    private async Task SkipOnboardingAsync()
    {
        if (_onboardingSaving)
            return;
        _onboardingSaving = true;
        var saved = await Auth.SetDashboardOnboardingDismissedAsync(true);
        _onboardingSaving = false;
        if (saved)
            _showOnboarding = false;
    }

    private bool IsOnboardingCompleted() =>
        Project.CurrentProject != null
        && _printersCount > 0
        && _storageCount > 0
        && _total > 0
        && _storedCount > 0;

    private static string GetBarClass(decimal percent) =>
        percent >= 50 ? "high" : percent >= 20 ? "medium" : "low";
}
