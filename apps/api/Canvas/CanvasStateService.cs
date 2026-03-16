namespace pixels_site.Api.Canvas;

public class CanvasStateService
{
    private static readonly string SavePath = Environment.GetEnvironmentVariable("CANVAS_SAVE_PATH") ?? "canvas.bin";

    private readonly byte[] _buffer;
    private readonly Lock _lock = new();
    private readonly Timer _saveTimer;
    private bool _dirty = false;

    public CanvasStateService()
    {
        int size = CanvasConfig.Width * CanvasConfig.Height * 4;
        _buffer = new byte[size];

        if (File.Exists(SavePath) && new FileInfo(SavePath).Length == size)
        {
            using var fs = File.OpenRead(SavePath);
            fs.ReadExactly(_buffer);
        }
        else
        {
            for (int i = 0; i < _buffer.Length; i += 4)
            {
                _buffer[i]     = 255;
                _buffer[i + 1] = 255;
                _buffer[i + 2] = 255;
                _buffer[i + 3] = 255;
            }
        }

        _saveTimer = new Timer(_ => PersistIfDirty(), null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
    }

    public void SetPixel(int x, int y, int r, int g, int b)
    {
        var idx = (y * CanvasConfig.Width + x) * 4;
        lock (_lock)
        {
            _buffer[idx]     = (byte)r;
            _buffer[idx + 1] = (byte)g;
            _buffer[idx + 2] = (byte)b;
            _dirty = true;
        }
    }

    public byte[] GetSnapshot()
    {
        lock (_lock)
            return _buffer.ToArray();
    }

    private void PersistIfDirty()
    {
        lock (_lock)
        {
            if (!_dirty) return;
            File.WriteAllBytes(SavePath, _buffer);
            _dirty = false;
        }
    }
}
