using System.Runtime.InteropServices;

namespace pixels_site.Api.Canvas;

public class CanvasStateService : IDisposable
{


    private readonly string savePath;
    private readonly Rgb[] pixels;
    private readonly Lock @lock = new();
    private readonly Timer saveTimer;
    private bool dirty;
    private readonly CanvasConfiguration config;
    private bool disposed;

    public CanvasStateService(CanvasConfiguration config)
    {
        this.config = config;
        int count = config.Width * config.Height;
        pixels = new Rgb[count];
        savePath = config.SavePath;
        int fileSize = count * 3;
        if (File.Exists(savePath) && new FileInfo(savePath).Length == fileSize)
        {
            byte[] bytes = File.ReadAllBytes(savePath);
            for (int i = 0; i < count; i++)
                pixels[i] = new Rgb { R = bytes[i * 3], G = bytes[i * 3 + 1], B = bytes[i * 3 + 2] }
            ;
        }
        else
        {
            var white = new Rgb { R = 255, G = 255, B = 255 };
            Array.Fill(pixels, white);
        }

        saveTimer = new Timer(_ => PersistIfDirty(), null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
    }

    public void SetPixel(int x, int y, Rgb Rgb)
    {
        int idx = y * config.Width + x;
        lock (@lock)
        {
            pixels[idx] = Rgb;
            dirty = true;
        }
    }

    public Rgb[] GetSnapshot()
    {
        lock (@lock)
        {
            var copy = new Rgb[pixels.Length];
            Array.Copy(pixels, copy, pixels.Length);
            return copy;
        }
    }

    private void PersistIfDirty()
    {
        lock (@lock)
        {
            if (!dirty) return;
            ReadOnlySpan<byte> bytes = MemoryMarshal.AsBytes(pixels.AsSpan());

            var tempPath = savePath + ".tmp";
            using (var fs = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                fs.Write(bytes);
                fs.Flush(flushToDisk: true);
            }

            File.Move(tempPath, savePath, overwrite: true);
            dirty = false;
        }
    }

    public void Dispose()
    {
        if (disposed) return;
        saveTimer.Dispose();
        PersistIfDirty();
        disposed = true;
        GC.SuppressFinalize(this);
    }
}
