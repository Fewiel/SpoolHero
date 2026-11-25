using System.Globalization;
using SpoolManager.Shared.DTOs.Spools;

namespace SpoolManager.Client.Helpers;

/// <summary>Small display helpers shared by the spool pages (dashboard, shelf, detail, locations).</summary>
public static class SpoolUi
{
    /// <summary>Below this remaining percentage a spool counts as "almost empty".</summary>
    public const decimal LowPercent = 20m;

    public static bool IsActive(SpoolDto s) => s.ConsumedAt == null;

    public static bool IsLow(SpoolDto s) => s.ConsumedAt == null && s.RemainingPercent < LowPercent;

    public static bool HasLocation(SpoolDto s) => s.PrinterId != null || s.StorageLocationId != null || s.DryerId != null;

    /// <summary>Short, human friendly id (first 5 hex chars) – also printed on tags.</summary>
    public static string ShortId(SpoolDto s) => s.Id.ToString("N")[..5].ToUpperInvariant();

    /// <summary>Main name of a spool, e.g. "PLA Galaxy Black" (or "Prusament PLA" when the colour has no name).</summary>
    public static string Title(SpoolDto s) =>
        string.IsNullOrWhiteSpace(s.MaterialColorName) ? $"{s.MaterialBrand} {s.MaterialType}".Trim() : $"{s.MaterialType} {s.MaterialColorName}";

    /// <summary>Full name incl. brand without repeating it, e.g. "Prusament PLA Galaxy Black" or "Prusament PLA".</summary>
    public static string FullTitle(SpoolDto s) =>
        string.IsNullOrWhiteSpace(s.MaterialColorName) ? Title(s) : $"{s.MaterialBrand} {Title(s)}".Trim();

    /// <summary>Colour name or hex value.</summary>
    public static string ColorLabel(SpoolDto s) =>
        string.IsNullOrWhiteSpace(s.MaterialColorName) ? "#" + s.MaterialColorHex : s.MaterialColorName!;

    public static string LocationName(SpoolDto s) => s.PrinterName ?? s.StorageLocationName ?? s.DryerName ?? string.Empty;

    public static string LocationIcon(SpoolDto s) =>
        s.PrinterId != null ? "bi-printer" : s.StorageLocationId != null ? "bi-box-seam" : s.DryerId != null ? "bi-thermometer-half" : "bi-question-circle";

    public static string Grams(decimal grams) =>
        grams.ToString(grams >= 100 ? "0" : "0.#", CultureInfo.CurrentCulture) + " g";

    /// <summary>Weight in g below 1 kg, otherwise in kg with one decimal.</summary>
    public static string Weight(decimal grams) =>
        grams >= 1000 ? (grams / 1000m).ToString("0.0", CultureInfo.CurrentCulture) + " kg" : Grams(grams);

    public static string Percent(decimal percent) =>
        Math.Round(Math.Clamp(percent, 0, 100), 0).ToString("0", CultureInfo.CurrentCulture) + " %";

    /// <summary>Drying state of a spool that sits in a dryer and has a drying start (DriedAt).</summary>
    public static DryingState? GetDrying(SpoolDto s)
    {
        if (s.DryerId == null || s.DriedAt == null || s.ConsumedAt != null)
            return null;
        var elapsed = DateTime.UtcNow - AsUtc(s.DriedAt.Value);
        if (elapsed < TimeSpan.Zero)
            elapsed = TimeSpan.Zero;
        if (s.MaterialDryTimeHours is not > 0)
            return new DryingState(elapsed, null, false);
        var total = TimeSpan.FromHours(s.MaterialDryTimeHours.Value);
        return new DryingState(elapsed, total, elapsed >= total);
    }

    /// <summary>The API sends UTC timestamps without a zone marker – treat "unspecified" as UTC.</summary>
    public static DateTime AsUtc(DateTime value) =>
        value.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(value, DateTimeKind.Utc) : value.ToUniversalTime();

    /// <summary>Server timestamp shown as local date, e.g. "14.10.2026".</summary>
    public static string LocalDate(DateTime value) => AsUtc(value).ToLocalTime().ToString("d", CultureInfo.CurrentCulture);

    /// <summary>Server timestamp shown as local date + time.</summary>
    public static string LocalDateTime(DateTime value) => AsUtc(value).ToLocalTime().ToString("g", CultureInfo.CurrentCulture);

    /// <summary>A pure calendar date (e.g. purchase date) – shown without time zone conversion.</summary>
    public static string PlainDate(DateTime value) => value.ToString("d", CultureInfo.CurrentCulture);

    /// <summary>"1 h 25 min" / "25 min".</summary>
    public static string Duration(TimeSpan t)
    {
        var minutes = (int)Math.Max(0, Math.Round(t.TotalMinutes));
        var h = minutes / 60;
        var m = minutes % 60;
        return h > 0 ? (m > 0 ? $"{h} h {m} min" : $"{h} h") : $"{m} min";
    }
}

public sealed record DryingState(TimeSpan Elapsed, TimeSpan? Total, bool Done)
{
    public int Percent => Total is { } t && t > TimeSpan.Zero ? (int)Math.Min(100, Elapsed.TotalMinutes / t.TotalMinutes * 100) : 0;

    public TimeSpan Remaining => Total is { } t && t > Elapsed ? t - Elapsed : TimeSpan.Zero;
}
