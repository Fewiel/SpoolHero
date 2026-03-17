namespace SpoolManager.Shared.DTOs.Materials;

public class MaterialSearchRequest
{
    public string? Query { get; set; }
    public string? MaterialType { get; set; }
    public string? Brand { get; set; }
    public bool? GlobalOnly { get; set; }
    public int Limit { get; set; } = 30;
    public int Offset { get; set; } = 0;
}

public class MaterialSearchResult
{
    public List<FilamentMaterialDto> Items { get; set; } = [];
    public int TotalCount { get; set; }
    public List<string> AvailableBrands { get; set; } = [];
    public List<string> AvailableTypes { get; set; } = [];
}
