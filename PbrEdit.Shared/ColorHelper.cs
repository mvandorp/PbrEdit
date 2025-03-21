namespace PbrEdit;

public static class ColorHelper
{
    public static uint GetMetalness(uint color) => color & 0xFF;
    public static uint GetGlossiness(uint color) => (color >> 24) & 0xFF;
    public static uint SetProperties(uint color, uint metalness, uint glossiness) => (color & 0x00FFFF00) | (glossiness << 24) | metalness;

    public static string ToRgbHexString(uint color) => $"#{color & 0xFFFFFF:X6}";

    public static uint FromRgbHexString(string value)
    {
        if (!value.StartsWith('#'))
            throw new ArgumentException($"Invalid color: {value}", nameof(value));

        return 0xFF000000 | uint.Parse(value[1..], System.Globalization.NumberStyles.HexNumber);
    }

    public static uint FromDouble(double value) => Math.Clamp((uint)Math.Round(value * 255.0), 0, 255);
}