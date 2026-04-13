using SpoolManager.Shared.Models;

namespace SpoolManager.Infrastructure.Services;

public record OpenSpoolParseResult(FilamentMaterial? Material, bool IsValid, string RawJson, Guid? SpoolId);
