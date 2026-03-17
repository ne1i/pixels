using System.Runtime.InteropServices;

namespace pixels_site.Api.Canvas;

public class CanvasStateService : IDisposable
{
    private static string ResolveSavePath()
    {
        var path = Environment.GetEnvironmentVariable("CANVAS_SAVE_PATH") ?? "canvas.bin";
        return Path.IsPathRooted(path) ? path : Path.Combine(AppContext.BaseDirectory, path);
    }

    private readonly string _savePath = ResolveSavePath();
    private readonly Rgb[] _pixels;
    private readonly Lock _lock = new();
    private readonly Timer _saveTimer;
    private bool _dirty;
    private readonly CanvasConfiguration _config;
    private bool _disposed;

    public CanvasStateService(CanvasConfiguration config)
    {
        _config = config;
        int count = config.Width * config.Height;
        _pixels = new Rgb[count];

        int fileSize = count * 3;
        if (File.Exists(_savePath) && new FileInfo(_savePath).Length == fileSize)
        {
            byte[] bytes = File.ReadAllBytes(_savePath);
            for (int i = 0; i < count; i++)
                _pixels[i] = new Rgb(bytes[i * 3], bytes[i * 3 + 1], bytes[i * 3 + 2]);
        }
        else
        {
            var white = new Rgb(255, 255, 255);
            Array.Fill(_pixels, white);
        }

        _saveTimer = new Timer(_ => PersistIfDirty(), null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
    }

    public void SetPixel(int x, int y, int r, int g, int b)
    {
        int idx = y * _config.Width + x;
        lock (_lock)
        {
            _pixels[idx] = new Rgb((byte)r, (byte)g, (byte)b);
            _dirty = true;
        }
    }

    public Rgb[] GetSnapshot()
    {
        lock (_lock)
        {
            var copy = new Rgb[_pixels.Length];
            Array.Copy(_pixels, copy, _pixels.Length);
            return copy;
        }
    }

    private void PersistIfDirty()
    {
        lock (_lock)
        {
            if (!_dirty) return;
            ReadOnlySpan<byte> bytes = MemoryMarshal.AsBytes(_pixels.AsSpan());
            using (var fs = new FileStream(_savePath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                fs.Write(bytes);
                fs.Flush(flushToDisk: true);
            }
            _dirty = false;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _saveTimer.Dispose();
        PersistIfDirty();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
