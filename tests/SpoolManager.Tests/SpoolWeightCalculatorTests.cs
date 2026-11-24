using SpoolManager.Shared.Helpers;

namespace SpoolManager.Tests;

public class SpoolWeightCalculatorTests
{
    [Theory]
    [InlineData(180.0, 250, 180.0)]
    [InlineData(null, 250, 250.0)]
    [InlineData(0.0, 250, 250.0)]
    [InlineData(null, null, null)]
    [InlineData(null, 0, null)]
    public void EffectiveTarePrefersSpoolValue(double? spoolTare, int? materialTare, double? expected)
    {
        Assert.Equal((decimal?)expected, SpoolWeightCalculator.EffectiveTare((decimal?)spoolTare, materialTare));
    }

    [Theory]
    [InlineData(1180, 180, 1000)]
    [InlineData(500.55, 180, 320.6)]
    [InlineData(100, 180, 0)]
    public void RemainingFromGrossSubtractsTare(double gross, double tare, double expected)
    {
        Assert.Equal((decimal)expected, SpoolWeightCalculator.RemainingFromGross((decimal)gross, (decimal)tare));
    }

    [Theory]
    [InlineData(500, 1000, 50)]
    [InlineData(1200, 1000, 100)]
    [InlineData(333, 1000, 33.3)]
    public void PercentIsRelativeToNominalWeight(double remaining, int nominal, double expected)
    {
        Assert.Equal((decimal)expected, SpoolWeightCalculator.Percent((decimal)remaining, nominal));
    }

    [Fact]
    public void PercentIsNullWithoutNominalWeight()
    {
        Assert.Null(SpoolWeightCalculator.Percent(500, null));
    }
}
