using System.Runtime.InteropServices;

namespace pixels_site.Api.Canvas;

public class CanvasStateService
{
    private static readonly string SavePath = Environment.GetEnvironmentVariable("CANVAS_SAVE_PATH") ?? "canvas.bin";

    private readonly Rgb[] _pixels;
    private readonly Lock _lock = new();
    private readonly Timer _saveTimer;
    private bool _dirty;
    private readonly CanvasConfiguration _config;

    public CanvasStateService(CanvasConfiguration config)
    {
        _config = config;
        int count = config.Width * config.Height;
        _pixels = new Rgb[count];

        int fileSize = count * 3;
        if (File.Exists(SavePath) && new FileInfo(SavePath).Length == fileSize)
        {
            byte[] bytes = File.ReadAllBytes(SavePath);
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
            using var fs = File.OpenWrite(SavePath);
            fs.Write(bytes);
            _dirty = false;
        }
    }
}
