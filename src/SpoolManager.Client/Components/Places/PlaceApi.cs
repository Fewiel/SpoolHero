using SpoolManager.Client.Services;
using SpoolManager.Shared.DTOs.Dryers;
using SpoolManager.Shared.DTOs.Printers;
using SpoolManager.Shared.DTOs.Storage;

namespace SpoolManager.Client.Components.Places;

/// <summary>
/// A place that holds spools (printer, storage location or dryer), normalised so that
/// <see cref="PlaceBoard"/> can render all three kinds the same way.
/// </summary>
public class PlaceItem
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public string? RfidTagUid { get; set; }
    public string? ImageBase64 { get; set; }
    public string? ImageContentType { get; set; }

    public PlaceItem Clone() => (PlaceItem)MemberwiseClone();
}

/// <summary>Kind-specific API access used by <see cref="PlaceBoard"/>.</summary>
public interface IPlaceApi
{
    /// <summary>Entity type as used by the tag API and the i18n keys: printer, storage or dryer.</summary>
    string Kind { get; }
    Task<List<PlaceItem>> GetAllAsync();
    Task<HttpResponseMessage> CreateAsync(PlaceItem form);
    Task<HttpResponseMessage> UpdateAsync(Guid id, PlaceItem form);
    Task<HttpResponseMessage> DeleteAsync(Guid id);
    Task<HttpResponseMessage> UploadImageAsync(Guid id, MultipartFormDataContent content);
    Task<HttpResponseMessage> DeleteImageAsync(Guid id);
}

public sealed class PrinterPlaceApi(PrinterService service) : IPlaceApi
{
    public string Kind => "printer";

    public async Task<List<PlaceItem>> GetAllAsync() =>
        (await service.GetAllAsync() ?? []).Select(p => new PlaceItem
        {
            Id = p.Id, Name = p.Name, Notes = p.Notes, RfidTagUid = p.RfidTagUid,
            ImageBase64 = p.ImageBase64, ImageContentType = p.ImageContentType
        }).ToList();

    public Task<HttpResponseMessage> CreateAsync(PlaceItem f) =>
        service.CreateAsync(new CreatePrinterRequest { Name = f.Name, Notes = f.Notes, RfidTagUid = f.RfidTagUid });

    public Task<HttpResponseMessage> UpdateAsync(Guid id, PlaceItem f) =>
        service.UpdateAsync(id, new UpdatePrinterRequest { Name = f.Name, Notes = f.Notes, RfidTagUid = f.RfidTagUid });

    public Task<HttpResponseMessage> DeleteAsync(Guid id) => service.DeleteAsync(id);
    public Task<HttpResponseMessage> UploadImageAsync(Guid id, MultipartFormDataContent c) => service.UploadImageAsync(id, c);
    public Task<HttpResponseMessage> DeleteImageAsync(Guid id) => service.DeleteImageAsync(id);
}

public sealed class StoragePlaceApi(StorageService service) : IPlaceApi
{
    public string Kind => "storage";

    public async Task<List<PlaceItem>> GetAllAsync() =>
        (await service.GetAllAsync() ?? []).Select(s => new PlaceItem
        {
            Id = s.Id, Name = s.Name, Notes = s.Description, RfidTagUid = s.RfidTagUid,
            ImageBase64 = s.ImageBase64, ImageContentType = s.ImageContentType
        }).ToList();

    public Task<HttpResponseMessage> CreateAsync(PlaceItem f) =>
        service.CreateAsync(new CreateStorageLocationRequest { Name = f.Name, Description = f.Notes, RfidTagUid = f.RfidTagUid });

    public Task<HttpResponseMessage> UpdateAsync(Guid id, PlaceItem f) =>
        service.UpdateAsync(id, new UpdateStorageLocationRequest { Name = f.Name, Description = f.Notes, RfidTagUid = f.RfidTagUid });

    public Task<HttpResponseMessage> DeleteAsync(Guid id) => service.DeleteAsync(id);
    public Task<HttpResponseMessage> UploadImageAsync(Guid id, MultipartFormDataContent c) => service.UploadImageAsync(id, c);
    public Task<HttpResponseMessage> DeleteImageAsync(Guid id) => service.DeleteImageAsync(id);
}

public sealed class DryerPlaceApi(DryerService service) : IPlaceApi
{
    public string Kind => "dryer";

    public async Task<List<PlaceItem>> GetAllAsync() =>
        (await service.GetAllAsync() ?? []).Select(d => new PlaceItem
        {
            Id = d.Id, Name = d.Name, Notes = d.Description, RfidTagUid = d.RfidTagUid,
            ImageBase64 = d.ImageBase64, ImageContentType = d.ImageContentType
        }).ToList();

    public Task<HttpResponseMessage> CreateAsync(PlaceItem f) =>
        service.CreateAsync(new CreateDryerRequest { Name = f.Name, Description = f.Notes, RfidTagUid = f.RfidTagUid });

    public Task<HttpResponseMessage> UpdateAsync(Guid id, PlaceItem f) =>
        service.UpdateAsync(id, new UpdateDryerRequest { Name = f.Name, Description = f.Notes, RfidTagUid = f.RfidTagUid });

    public Task<HttpResponseMessage> DeleteAsync(Guid id) => service.DeleteAsync(id);
    public Task<HttpResponseMessage> UploadImageAsync(Guid id, MultipartFormDataContent c) => service.UploadImageAsync(id, c);
    public Task<HttpResponseMessage> DeleteImageAsync(Guid id) => service.DeleteImageAsync(id);
}
