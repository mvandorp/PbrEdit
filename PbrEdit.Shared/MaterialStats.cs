namespace PbrEdit;

public record class MaterialStats
{
    public virtual double MetalnessMin { get; init; }
    public virtual double MetalnessMax { get; init; }
    public virtual double MetalnessAverage { get; init; }
    public virtual double GlossinessMin { get; init; }
    public virtual double GlossinessMax { get; init; }
    public virtual double GlossinessAverage { get; init; }
}

public sealed record class MaterialStatsBuilder : MaterialStats
{
    private int _count;
    private double _metalnessAccumulator;
    private double _metalnessMin = double.PositiveInfinity;
    private double _metalnessMax = double.NegativeInfinity;
    private double _glossinessAccumulator;
    private double _glossinessMin = double.PositiveInfinity;
    private double _glossinessMax = double.NegativeInfinity;

    public override double MetalnessMin => Math.Round(_metalnessMin / 255.0, 2);
    public override double MetalnessMax => Math.Round(_metalnessMax / 255.0, 2);
    public override double MetalnessAverage => Math.Round(_metalnessAccumulator / (_count * 255), 2);
    public override double GlossinessMin => Math.Round(_glossinessMin / 255.0, 2);
    public override double GlossinessMax => Math.Round(_glossinessMax / 255.0, 2);
    public override double GlossinessAverage => Math.Round(_glossinessAccumulator / (_count * 255), 2);

    public void AddPixel(uint color)
    {
        var metalness = ColorHelper.GetMetalness(color);
        var glossiness = ColorHelper.GetGlossiness(color);

        _metalnessAccumulator += metalness;
        _metalnessMin = Math.Min(_metalnessMin, metalness);
        _metalnessMax = Math.Max(_metalnessMax, metalness);
        _glossinessAccumulator += glossiness;
        _glossinessMin = Math.Min(_glossinessMin, glossiness);
        _glossinessMax = Math.Max(_glossinessMax, glossiness);
        _count++;
    }
}
