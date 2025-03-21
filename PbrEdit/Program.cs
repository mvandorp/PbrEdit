using PbrEdit;
using System.Collections.Immutable;
using System.Drawing;
using System.Text.Json;
using System.Text.Json.Serialization;

if (!OperatingSystem.IsWindowsVersionAtLeast(6, 1))
    return;

#if true
var path = @"test.json";
#else
var path = args.FirstOrDefault();
#endif

if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
    return;

try
{
    var json = File.ReadAllText(path);
    var settings = JsonSerializer.Deserialize<Settings>(json);
    if (settings is null)
        return;

    var inputDirectory = Path.GetFullPath(settings.InputDirectory);
    var outputDirectory = Path.GetFullPath(settings.OutputDirectory);
    var maskDirectory = Path.GetFullPath(settings.MaskDirectory);

    foreach (var kvp in settings.Input)
    {
        var fileName = Path.GetFileName(kvp.Key);

        Process(inputDirectory, outputDirectory, maskDirectory, fileName, kvp.Value, settings.Materials);
    }
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex.ToString());
    Console.ReadKey();
}

static void Process(string inputDirectory, string outputDirectory, string maskDirectory, string fileName, ImageSettings settings, IReadOnlyDictionary<string, Material> materials)
{
    if (settings.MaskToMaterial.Values.FirstOrDefault(material => !materials.ContainsKey(material)) is string undefinedMaterial)
        throw new InvalidOperationException($"Undefined material '{undefinedMaterial}': {fileName}");

    var inputPath = Path.Combine(inputDirectory, fileName);
    var outputPath = Path.Combine(outputDirectory, fileName);
    var maskPath = Path.Combine(maskDirectory, Path.ChangeExtension(fileName, "png"));

    var colorToMaskPath = Path.Combine(maskDirectory, Path.ChangeExtension(fileName, "json"));
    var colorToMask = MaskHelper.ReadColorToMaskMap(colorToMaskPath);
    if (colorToMask is null)
        throw new InvalidOperationException($"Could not read {colorToMaskPath}");

    if (colorToMask.Values.FirstOrDefault(mask => !settings.MaskToMaterial.ContainsKey(mask)) is string maskWithoutMaterial)
        throw new InvalidOperationException($"No material defined for mask '{maskWithoutMaterial}': {fileName}");

    var statsPath = Path.Combine(maskDirectory, Path.ChangeExtension(fileName, "stats.json"));
    var stats = MaskHelper.ReadMaskStats(statsPath);
    if (stats is null)
    {
        if (colorToMask.Values.FirstOrDefault(mask => materials[settings.MaskToMaterial[mask]].ContainsAverage) is string maskWithAverage)
            throw new InvalidOperationException($"Material '{settings.MaskToMaterial[maskWithAverage]}' for mask '{maskWithAverage}' contains an average, but could not find {statsPath}: {fileName}");
    }

    var colorToMaterial = MaskHelper.CreateColorToMaterialMap(colorToMask, settings.MaskToMaterial, materials, stats);

    using var bitmap = new Bitmap(inputPath);
    using var buffer = new BitmapBuffer(bitmap);
    using var mask = BitmapBuffer.FromFile(maskPath);

    if (settings.Format != ImageFormat.RGBA)
    {
        throw new NotSupportedException($"Only RGBA normal maps are supported at this time: {fileName}");
    }

    if (buffer.Width != mask.Width || buffer.Height != mask.Height)
    {
        throw new InvalidOperationException($"Dimension of the input image and mask do not match: {fileName}");
    }

    for (var y = 0; y < mask.Height; y++)
    {
        for (var x = 0; x < mask.Width; x++)
        {
            var color = buffer.GetPixel(x, y);
            var maskColor = mask.GetPixel(x, y);

            if (colorToMaterial.TryGetValue(maskColor, out var material))
            {
                buffer.SetPixel(x, y, ApplyMaterial(color, material));
            }
            else
            {
                throw new InvalidOperationException($"Could not map color {ColorHelper.ToRgbHexString(color)} to any mask: {fileName}");
            }
        }
    }

    buffer.CopyTo(bitmap);
    bitmap.Save(outputPath, System.Drawing.Imaging.ImageFormat.Png);
}

static uint ApplyMaterial(uint color, Material material)
{
    var metalness = ColorHelper.GetMetalness(color);
    var glossiness = ColorHelper.GetGlossiness(color);

    if (material.MetalnessMultiplier != 1.0)
        metalness = Math.Clamp((uint)Math.Round(metalness * material.MetalnessMultiplier), 0, 255);

    if (material.GlossinessMultiplier != 1.0)
        glossiness = Math.Clamp((uint)Math.Round(glossiness * material.GlossinessMultiplier), 0, 255);

    metalness = Math.Clamp(metalness, material.MetalnessMinByte, material.MetalnessMaxByte);
    glossiness = Math.Clamp(glossiness, material.GlossinessMinByte, material.GlossinessMaxByte);

    return ColorHelper.SetProperties(color, metalness, glossiness);
}

sealed record class Settings
{
    private readonly Lazy<IReadOnlyDictionary<string, Material>> _materials;

    public Settings()
    {
        _materials = new Lazy<IReadOnlyDictionary<string, Material>>(() =>
            MaterialDefinitions.ToImmutableDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value.GetMaterial(Variables)
            ), isThreadSafe: true);
    }

    public string InputDirectory { get; init; } = string.Empty;
    public string OutputDirectory { get; init; } = string.Empty;
    public string MaskDirectory { get; init; } = string.Empty;

    public IReadOnlyDictionary<string, double> Variables { get; init; } = ImmutableDictionary<string, double>.Empty;
    [JsonPropertyName(nameof(Materials))]
    public IReadOnlyDictionary<string, MaterialSettings> MaterialDefinitions { get; init; } = ImmutableDictionary<string, MaterialSettings>.Empty;

    public IReadOnlyDictionary<string, ImageSettings> Input { get; init; } = ImmutableDictionary<string, ImageSettings>.Empty;

    [JsonIgnore]
    public IReadOnlyDictionary<string, Material> Materials => _materials.Value;
}


sealed record class ImageSettings
{
    public ImageFormat Format { get; init; } = ImageFormat.RGBA;
    public IReadOnlyDictionary<string, string> MaskToMaterial { get; init; } = ImmutableDictionary<string, string>.Empty;
}

[JsonConverter(typeof(JsonStringEnumConverter<ImageFormat>))]
enum ImageFormat
{
    RGB,
    RGBA
}

#region JSON
sealed record class MaterialSettings
{
    public IValue? Metalness { get; init; }
    public IValue MetalnessMin { get; init; } = new ConstantValue(0.0);
    public IValue MetalnessMax { get; init; } = new ConstantValue(1.0);
    public IValue MetalnessMultiplier { get; init; } = new ConstantValue(1.0);
    public IValue? MetalnessAverage { get; init; }
    public IValue? Glossiness { get; init; }
    public IValue GlossinessMin { get; init; } = new ConstantValue(0.0);
    public IValue GlossinessMax { get; init; } = new ConstantValue(1.0);
    public IValue GlossinessMultiplier { get; init; } = new ConstantValue(1.0);
    public IValue? GlossinessAverage { get; init; }

    public Material GetMaterial(IReadOnlyDictionary<string, double> variables)
    {
        return new(
            Math.Max(MetalnessMin.GetValue(variables), Metalness?.GetValue(variables) ?? 0.0),
            Math.Min(MetalnessMax.GetValue(variables), Metalness?.GetValue(variables) ?? 1.0),
            MetalnessMultiplier.GetValue(variables),
            MetalnessAverage?.GetValue(variables),
            Math.Max(GlossinessMin.GetValue(variables), Glossiness?.GetValue(variables) ?? 0.0),
            Math.Min(GlossinessMax.GetValue(variables), Glossiness?.GetValue(variables) ?? 1.0),
            GlossinessMultiplier.GetValue(variables),
            GlossinessAverage?.GetValue(variables));
    }
}

[JsonConverter(typeof(IValueConverter))]
interface IValue
{
    double GetValue(IReadOnlyDictionary<string, double> variables);
}

readonly record struct VariableValue(string Name) : IValue
{
    public double GetValue(IReadOnlyDictionary<string, double> variables)
    {
        if (variables.TryGetValue(Name, out var value))
            return value;
        else
            throw new KeyNotFoundException($"Unknown variable: {Name}");
    }
}

readonly record struct ConstantValue(double Value) : IValue
{
    public double GetValue(IReadOnlyDictionary<string, double> variables)
    {
        return Value;
    }
}

sealed class IValueConverter : JsonConverter<IValue>
{
    public override IValue? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            JsonTokenType.Number => new ConstantValue(reader.GetDouble()),
            JsonTokenType.String => new VariableValue(reader.GetString()!),
            _ => throw new JsonException($"Unexpected token type: {reader.TokenType}"),
        };
    }

    public override void Write(Utf8JsonWriter writer, IValue value, JsonSerializerOptions options)
    {
        if (value is ConstantValue constant)
            writer.WriteNumberValue(constant.Value);
        else if (value is VariableValue variable)
            writer.WriteStringValue(variable.Name);
        else
            throw new JsonException($"Unsupported IValue type '{value.GetType().Name}'. Only {nameof(ConstantValue)} and {nameof(VariableValue)} are supported");
    }
}
#endregion