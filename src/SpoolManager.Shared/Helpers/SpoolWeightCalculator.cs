namespace SpoolManager.Shared.Helpers;

/// <summary>
/// Converts scale readings (filament + empty spool) into remaining filament.
/// </summary>
public static class SpoolWeightCalculator
{
    /// <summary>The empty spool weight to use: the spool's own value wins over the material default.</summary>
    public static decimal? EffectiveTare(decimal? spoolTare, int? materialTare) =>
        spoolTare is > 0 ? spoolTare : materialTare is > 0 ? materialTare : null;

    /// <summary>Remaining filament in grams for a gross scale reading.</summary>
    public static decimal RemainingFromGross(decimal grossGrams, decimal? tareGrams) =>
        Math.Round(Math.Max(0, grossGrams - (tareGrams ?? 0)), 1);

    /// <summary>Remaining percentage relative to the nominal filament weight, or null if unknown.</summary>
    public static decimal? Percent(decimal remainingGrams, int? nominalGrams) =>
        nominalGrams is > 0 ? Math.Round(Math.Min(100, remainingGrams / nominalGrams.Value * 100m), 1) : null;
}
