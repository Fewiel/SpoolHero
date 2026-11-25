using Microsoft.AspNetCore.Components;
using SpoolManager.Client.Components.Places;
using SpoolManager.Client.Services;

namespace SpoolManager.Client.Pages.Printers;

public partial class PrinterList
{
    [Inject] private PrinterService Printers { get; set; } = default!;

    private IPlaceApi _api = default!;

    protected override void OnInitialized() => _api = new PrinterPlaceApi(Printers);
}
