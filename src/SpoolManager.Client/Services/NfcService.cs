using Microsoft.JSInterop;

namespace SpoolManager.Client.Services;

public class NfcService
{
    private readonly IJSRuntime _js;

    public bool IsSupported { get; private set; }

    public NfcService(IJSRuntime js) => _js = js;

    public async Task<bool> CheckSupportAsync()
    {
        try { IsSupported = await _js.InvokeAsync<bool>("nfcHelper.isSupported"); }
        catch { IsSupported = false; }
        return IsSupported;
    }

    public async Task WriteAsync<T>(string jsonPayload, DotNetObjectReference<T> dotnetRef, int? spoolmanId = null) where T : class
    {
        await _js.InvokeVoidAsync("nfcHelper.write", jsonPayload, dotnetRef, spoolmanId);
    }

    public async Task StartReadAsync<T>(DotNetObjectReference<T> dotnetRef) where T : class
    {
        await _js.InvokeVoidAsync("nfcHelper.read", dotnetRef);
    }

    public async Task StopReadAsync()
    {
        await _js.InvokeVoidAsync("nfcHelper.stop");
    }
}
