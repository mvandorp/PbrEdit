using PbrEdit;
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
    var maskDirectory = Path.GetFullPath(settings.MaskDirectory);

    foreach (var fileName in settings.Input)
    {
        var outputPath = Path.Combine(maskDirectory, Path.ChangeExtension(fileName, "stats.json"));
        var stats = MaskHelper.Analyze(inputDirectory, maskDirectory, fileName);
        var statsJson = JsonSerializer.Serialize(stats, typeof(IDictionary<string, MaterialStatsBuilder>), SourceGenerationContext.Default);
        File.WriteAllText(outputPath, statsJson);
    }
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex.ToString());
    Console.ReadKey();
}

sealed record class Settings
{
    public string InputDirectory { get; init; } = string.Empty;
    public string MaskDirectory { get; init; } = string.Empty;
    public IReadOnlyCollection<string> Input { get; init; } = [];
}


[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(IDictionary<string, MaterialStatsBuilder>))]
partial class SourceGenerationContext : JsonSerializerContext
{
}
