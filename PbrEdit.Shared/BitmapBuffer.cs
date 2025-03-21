using System.Buffers;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.Versioning;

namespace PbrEdit;

public sealed unsafe class BitmapBuffer : IDisposable
{
    public const int BYTES_PER_PIXEL = 4;

    public Memory<byte> Data { get; init; }
    public MemoryHandle Handle { get; init; }
    public unsafe byte* Scan0 { get; init; }
    public int Width { get; init; }
    public int Height { get; init; }
    public int Stride { get; init; }

    public void Dispose()
    {
        Handle.Dispose();
        GC.SuppressFinalize(this);
    }

    private BitmapBuffer()
    {
    }

    [SupportedOSPlatform("Windows")]
    public BitmapBuffer(Bitmap bitmap) : this(bitmap?.Width ?? 0, bitmap?.Height ?? 0)
    {
        Debug.Assert(bitmap != null);
        CopyFrom(bitmap);
    }

    public BitmapBuffer(int width, int height)
    {
        var stride = width * BYTES_PER_PIXEL;
        var buffer = (Memory<byte>)new byte[height * stride];
        var handle = buffer.Pin();

        Data = buffer;
        Handle = handle;
        Width = width;
        Height = height;
        Stride = stride;
        Scan0 = (byte*)handle.Pointer;
    }

    [SupportedOSPlatform("Windows")]
    public static BitmapBuffer FromFile(string filename)
    {
        using var image = new Bitmap(filename);
        return new BitmapBuffer(image);
    }

    public uint GetPixel(int x, int y) => *(uint*)GetPointer(x, y);
    public void SetPixel(int x, int y, uint value) => *(uint*)GetPointer(x, y) = value;

    private unsafe byte* GetPointer(int x, int y)
    {
        Debug.Assert(x <= Width);
        Debug.Assert(y <= Height);

        return &Scan0[y * Stride + x * BYTES_PER_PIXEL];
    }

    [SupportedOSPlatform("Windows")]
    public void CopyTo(Bitmap bitmap)
    {
        Debug.Assert(bitmap != null);
        Debug.Assert(Width == bitmap.Width);
        Debug.Assert(Height == bitmap.Height);

        var data = bitmap.LockBits(new Rectangle(0, 0, Width, Height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        var numBytes = Height * Stride;
        try
        {
            Debug.Assert(Stride == data.Stride);

            Buffer.MemoryCopy(Scan0, data.Scan0.ToPointer(), numBytes, numBytes);
        }
        finally
        {
            bitmap.UnlockBits(data);
        }
    }

    [SupportedOSPlatform("Windows")]
    public void CopyFrom(Bitmap bitmap)
    {
        Debug.Assert(bitmap != null);
        Debug.Assert(Width == bitmap.Width);
        Debug.Assert(Height == bitmap.Height);

        var data = bitmap.LockBits(new Rectangle(0, 0, Width, Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        var numBytes = Height * Stride;
        try
        {
            Debug.Assert(Stride == data.Stride);

            Buffer.MemoryCopy(data.Scan0.ToPointer(), Scan0, numBytes, numBytes);
        }
        finally
        {
            bitmap.UnlockBits(data);
        }
    }
}