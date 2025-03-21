using System.Collections.Immutable;
using System.Runtime.Versioning;
using System.Text.Json;

namespace PbrEdit;

public static class MaskHelper
{
    public static IReadOnlyDictionary<uint, Material> CreateColorToMaterialMap(
        IReadOnlyDictionary<uint, string> colorToMask,
        IReadOnlyDictionary<string, string> maskToMaterial,
        IReadOnlyDictionary<string, Material> materials,
        IReadOnlyDictionary<string, MaterialStats>? materialStats)
    {
        return colorToMask.ToImmutableDictionary(kvp => kvp.Key, kvp => ModifyMaterial(materials[maskToMaterial[kvp.Value]], materialStats?[kvp.Value]));
    }

    private static Material ModifyMaterial(Material material, MaterialStats? stats)
    {
        return new Material(
            material.MetalnessMin, material.MetalnessMax, SimplifyMultiplier(material.MetalnessMultiplier, material.MetalnessAverage, stats?.MetalnessAverage), null,
            material.GlossinessMin, material.GlossinessMax, SimplifyMultiplier(material.GlossinessMultiplier, material.GlossinessAverage, stats?.GlossinessAverage), null);
    }

    private static double SimplifyMultiplier(double multiplier, double? desiredAverage, double? actualAverage)
    {
        if (desiredAverage is null)
        {
            return multiplier;
        }
        else if (actualAverage is double actual && actual < (1.0 / 255.0))
        {
            Console.WriteLine("Warning: Actual average is too small or zero. Using default multiplier.");
            return multiplier;
        }
        else
        {
            return multiplier * ((desiredAverage ?? 1.0) / (actualAverage ?? 1.0));
        }
    }

    public static IReadOnlyDictionary<uint, string>? ReadColorToMaskMap(string path)
    {
        if (File.Exists(path))
        {
            var maskMapJson = File.ReadAllText(path);
            var maskMap = JsonSerializer.Deserialize<IDictionary<string, string>>(maskMapJson);
            if (maskMap is not null)
                return maskMap.ToImmutableDictionary(kvp => ColorHelper.FromRgbHexString(kvp.Key), kvp => kvp.Value);
        }

        return null;
    }

    public static IReadOnlyDictionary<string, MaterialStats>? ReadMaskStats(string path)
    {
        if (File.Exists(path))
        {
            var statsJson = File.ReadAllText(path);
            var stats = JsonSerializer.Deserialize<ImmutableDictionary<string, MaterialStats>>(statsJson);
            if (stats is not null)
                return stats;
        }

        return null;
    }

    [SupportedOSPlatform("Windows")]
    public static IDictionary<string, MaterialStatsBuilder> Analyze(string inputDirectory, string maskDirectory, string fileName)
    {
        var inputPath = Path.Combine(inputDirectory, fileName);
        var maskPath = Path.Combine(maskDirectory, Path.ChangeExtension(fileName, "png"));
        var maskMapPath = Path.Combine(maskDirectory, Path.ChangeExtension(fileName, "json"));
        var colorToMask = ReadColorToMaskMap(maskMapPath);
        if (colorToMask is null)
            throw new InvalidOperationException($"Could not read {maskMapPath}");

        using var input = BitmapBuffer.FromFile(inputPath);
        using var mask = BitmapBuffer.FromFile(maskPath);

        if (input.Width != mask.Width || input.Height != mask.Height)
        {
            throw new InvalidOperationException($"Dimension of the input image and mask do not match: {inputPath}");
        }

        var stats = new Dictionary<string, MaterialStatsBuilder>();
        foreach (var material in colorToMask.Values)
        {
            stats.Add(material, new MaterialStatsBuilder());
        }

        for (var y = 0; y < mask.Height; y++)
        {
            for (var x = 0; x < mask.Width; x++)
            {
                var color = input.GetPixel(x, y);
                var maskColor = mask.GetPixel(x, y);

                if (colorToMask.TryGetValue(maskColor, out var material))
                {
                    stats[material].AddPixel(color);
                }
                else
                {
                    throw new InvalidOperationException($"Could not map color {ColorHelper.ToRgbHexString(color)} at ({x}, {y}) to material: {inputPath}");
                }
            }
        }

        return stats;
    }

}
