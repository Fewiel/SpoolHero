using Microsoft.AspNetCore.Components;
using SpoolManager.Client.Components.Places;
using SpoolManager.Client.Services;

namespace SpoolManager.Client.Pages.Storage;

public partial class StorageList
{
    [Inject] private StorageService Storage { get; set; } = default!;

    private IPlaceApi _api = default!;

    protected override void OnInitialized() => _api = new StoragePlaceApi(Storage);
}
