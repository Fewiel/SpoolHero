using System.Net.Http.Json;

namespace SpoolManager.Client.Services;

public class FavoriteService
{
    private readonly HttpClient _http;
    private HashSet<Guid>? _cache;

    public FavoriteService(HttpClient http) => _http = http;

    public async Task EnsureCacheAsync()
    {
        if (_cache != null)
            return;
        var ids = await _http.GetFromJsonAsync<List<Guid>>("api/favorites") ?? [];
        _cache = new HashSet<Guid>(ids);
    }

    public void InvalidateCache() => _cache = null;

    public bool IsFavorite(Guid materialId) => _cache?.Contains(materialId) ?? false;

    public async Task<HashSet<Guid>> GetFavoriteIdsAsync()
    {
        await EnsureCacheAsync();
        return _cache!;
    }

    public async Task ToggleAsync(Guid materialId)
    {
        await EnsureCacheAsync();
        if (_cache!.Contains(materialId))
        {
            await _http.DeleteAsync($"api/favorites/{materialId}");
            _cache.Remove(materialId);
        }
        else
        {
            await _http.PostAsync($"api/favorites/{materialId}", null);
            _cache.Add(materialId);
        }
    }
}
