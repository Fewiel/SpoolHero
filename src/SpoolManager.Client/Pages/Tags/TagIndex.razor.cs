using Microsoft.AspNetCore.Components;
using SpoolManager.Client.Services;

namespace SpoolManager.Client.Pages.Tags;

public partial class TagIndex
{
    [Inject] private LocalizationService L { get; set; } = default!;
    [Inject] private NfcService Nfc { get; set; } = default!;

    private bool? _nfcSupported;

    protected override async Task OnInitializedAsync()
    {
        _nfcSupported = await Nfc.CheckSupportAsync();
    }
}
