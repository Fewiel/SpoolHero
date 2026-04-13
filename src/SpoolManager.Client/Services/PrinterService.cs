using System.Net.Http.Json;
using SpoolManager.Shared.DTOs.Printers;

namespace SpoolManager.Client.Services;

public class PrinterService
{
    private readonly HttpClient _http;
    public PrinterService(HttpClient http) => _http = http;

    public Task<List<PrinterDto>?> GetAllAsync() => _http.GetFromJsonAsync<List<PrinterDto>>("api/printers");

    public Task<HttpResponseMessage> CreateAsync(CreatePrinterRequest request) => _http.PostAsJsonAsync("api/printers", request);

    public Task<HttpResponseMessage> UpdateAsync(Guid id, UpdatePrinterRequest request) => _http.PutAsJsonAsync($"api/printers/{id}", request);

    public Task<HttpResponseMessage> DeleteAsync(Guid id) => _http.DeleteAsync($"api/printers/{id}");

    public Task<HttpResponseMessage> UploadImageAsync(Guid id, MultipartFormDataContent content) =>
        _http.PostAsync($"api/printers/{id}/image", content);

    public Task<HttpResponseMessage> DeleteImageAsync(Guid id) =>
        _http.DeleteAsync($"api/printers/{id}/image");
}
