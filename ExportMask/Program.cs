using ImageMagick;
using PbrEdit;
using System.Drawing;
using System.Drawing.Imaging;
using System.Text.Json;
using System.Text.Json.Serialization;

if (!OperatingSystem.IsWindowsVersionAtLeast(6, 1))
    return;

try
{
#if DEBUG
    var path = @"C:\Users\Martijn\MD-11\Masks\MD-11-cockpit-02_NRM.xcf";
#else
    foreach (var path in args)
#endif
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return;

        var directory = Path.GetDirectoryName(path) ?? Environment.CurrentDirectory;
        var fileName = Path.GetFileNameWithoutExtension(path);
        var pathMask = Path.Combine(directory, Path.ChangeExtension(fileName, "png"));
        var pathMap = Path.Combine(directory, Path.ChangeExtension(fileName, "json"));

        Console.WriteLine($"Loading {path}...");
        using var layers = new MagickImageCollection(path);

        Console.WriteLine("Writing layers to mask...");
        using var mask = CreateMask(layers, out var layerMap);
        layers.Dispose();

        Console.WriteLine($"Writing {pathMap}");
        File.WriteAllText(pathMap, JsonSerializer.Serialize(layerMap, typeof(IDictionary<string, string>), SourceGenerationContext.Default));

        Console.WriteLine($"Writing {pathMask}");
        using var maskBitmap = new Bitmap(mask.Width, mask.Height, PixelFormat.Format32bppArgb);
        mask.CopyTo(maskBitmap);
        maskBitmap.Save(pathMask, ImageFormat.Png);
    }
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex.ToString());
    Console.ReadKey();
}

static BitmapBuffer CreateMask(IMagickImageCollection<byte> layers, out IDictionary<string, string> layerMap)
{
    var maskIndex = 0;
    layerMap = new Dictionary<string, string>();

    var width = layers.FirstOrDefault()?.Width ?? 0;
    var height = layers.FirstOrDefault()?.Height ?? 0;
    var buffer = new BitmapBuffer((int)width, (int)height);
    using var mask = new BitmapBuffer((int)width, (int)height);
    using var stream = new MemoryStream();

    foreach (var layer in layers)
    {
        if (layer.Label is null)
        {
            layer.Dispose();
            continue;
        }

        if (layer.Label.StartsWith("MASK_"))
        {
            VerifyChannelCount(layer);
            VerifyDimensions(layer, width, height);

            var color = Constants.Colors[maskIndex++];
            var name = layer.Label;
            layer.Write(stream, MagickFormat.Bmp);
            layer.Dispose();

            using var bitmap = new Bitmap(stream);
            stream.Seek(0, SeekOrigin.Begin);

            mask.CopyFrom(bitmap);

            DrawMask(buffer, mask, color);

            layerMap.Add(ColorHelper.ToRgbHexString(color), name);
            Console.WriteLine($"{ColorHelper.ToRgbHexString(color)}: {name}");
        }

        if (maskIndex == Constants.ColorCount)
            throw new NotSupportedException($"Only up to {Constants.ColorCount} layers are supported");
    }

    return buffer;
}
static void VerifyChannelCount(IMagickImage<byte> image)
{
    if (image.ChannelCount != 4)
        throw new NotSupportedException("Only images with 4 channels are supported");
}

static void VerifyDimensions(IMagickImage<byte> image, uint expectedWidth, uint expectedHeight)
{
    if (image.Width != expectedWidth || image.Height != expectedHeight)
        throw new NotSupportedException($"Layer {image.Label} does not have the expected dimensions of {expectedWidth} x {expectedHeight}");
}

static void DrawMask(BitmapBuffer buffer, BitmapBuffer mask, uint color)
{
    for (var y = 0; y < mask.Height; y++)
        for (var x = 0; x < mask.Width; x++)
            if (mask.GetPixel(x, y) >> 24 > 0)
                buffer.SetPixel(x, y, color);
}

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(IDictionary<string, string>))]
[JsonSerializable(typeof(IDictionary<string, MaterialStatsBuilder>))]
partial class SourceGenerationContext : JsonSerializerContext
{
}

static class Constants
{
    public static uint[] Colors { get; } = [
        0xFF000000,
        0xFF00FF00,
        0xFF0000FF,
        0xFFFF0000,
        0xFF01FFFE,
        0xFFFFA6FE,
        0xFFFFDB66,
        0xFF006401,
        0xFF010067,
        0xFF95003A,
        0xFF007DB5,
        0xFFFF00F6,
        0xFFFFEEE8,
        0xFF774D00,
        0xFF90FB92,
        0xFF0076FF,
        0xFFD5FF00,
        0xFFFF937E,
        0xFF6A826C,
        0xFFFF029D,
        0xFFFE8900,
        0xFF7A4782,
        0xFF7E2DD2,
        0xFF85A900,
        0xFFFF0056,
        0xFFA42400,
        0xFF00AE7E,
        0xFF683D3B,
        0xFFBDC6FF,
        0xFF263400,
        0xFFBDD393,
        0xFF00B917,
        0xFF9E008E,
        0xFF001544,
        0xFFC28C9F,
        0xFFFF74A3,
        0xFF01D0FF,
        0xFF004754,
        0xFFE56FFE,
        0xFF788231,
        0xFF0E4CA1,
        0xFF91D0CB,
        0xFFBE9970,
        0xFF968AE8,
        0xFFBB8800,
        0xFF43002C,
        0xFFDEFF74,
        0xFF00FFC6,
        0xFFFFE502,
        0xFF620E00,
        0xFF008F9C,
        0xFF98FF52,
        0xFF7544B1,
        0xFFB500FF,
        0xFF00FF78,
        0xFFFF6E41,
        0xFF005F39,
        0xFF6B6882,
        0xFF5FAD4E,
        0xFFA75740,
        0xFFA5FFD2,
        0xFFFFB167,
        0xFF009BFF,
        0xFFE85EBE
    ];

    public static int ColorCount { get; } = Colors.Length;
}