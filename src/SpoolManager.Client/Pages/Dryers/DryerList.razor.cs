using Microsoft.AspNetCore.Components;
using SpoolManager.Client.Components.Places;
using SpoolManager.Client.Services;

namespace SpoolManager.Client.Pages.Dryers;

public partial class DryerList
{
    [Inject] private DryerService Dryers { get; set; } = default!;

    private IPlaceApi _api = default!;

    protected override void OnInitialized() => _api = new DryerPlaceApi(Dryers);
}
