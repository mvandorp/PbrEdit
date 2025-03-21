using System.Text.Json.Serialization;

namespace PbrEdit;

public sealed record class Material(
    double MetalnessMin, double MetalnessMax, double MetalnessMultiplier, double? MetalnessAverage,
    double GlossinessMin, double GlossinessMax, double GlossinessMultiplier, double? GlossinessAverage)
{
    public uint MetalnessMinByte { get; } = ColorHelper.FromDouble(MetalnessMin);
    public uint MetalnessMaxByte { get; } = ColorHelper.FromDouble(MetalnessMax);
    public uint GlossinessMinByte { get; } = ColorHelper.FromDouble(GlossinessMin);
    public uint GlossinessMaxByte { get; } = ColorHelper.FromDouble(GlossinessMax);

    public bool ContainsAverage => MetalnessAverage is not null || GlossinessAverage is not null;
}