public class CanvasConfiguration(IConfiguration configuration)
{
    public int Width { get; set; } = configuration.GetValue<int>("CanvasConfig:Width");
    public int Height { get; set; } = configuration.GetValue<int>("CanvasConfig:Height");
}