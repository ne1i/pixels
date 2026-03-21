using Microsoft.AspNetCore.SignalR;
using pixels_site.Api.Canvas;

namespace pixels_site.Api.Hubs;

public class CanvasHub(CanvasStateService canvasState, CanvasConfiguration config, ILogger<CanvasHub> logger) : Hub
{
    public async Task PlacePixel(PixelPlacementRequest request)
    {
        logger.LogInformation("PlacePixel received: ({X}, {Y}) rgb({R}, {G}, {B})", request.X, request.Y, request.Rgb.R, request.Rgb.G, request.Rgb.B);

        ValidateCoordinates(request.X, request.Y);
        ValidateColor(request.Rgb);

        canvasState.SetPixel(request.X, request.Y, request.Rgb);

        logger.LogInformation("Pixel placed at ({X}, {Y}) with color rgb({RGB}) by {ConnectionId}",
            request.X, request.Y, request.Rgb, Context.ConnectionId);

        await Clients.All.SendAsync("PixelPlaced", new PixelPlacedEvent(request.X, request.Y, request.Rgb));
    }

    public async Task PlacePixels(IReadOnlyList<PixelPlacementRequest> requests)
    {
        if (requests.Count == 0)
            return;

        var appliedPixels = new List<PixelPlacedEvent>(requests.Count);

        foreach (var request in requests)
        {
            ValidateCoordinates(request.X, request.Y);
            ValidateColor(request.Rgb);

            canvasState.SetPixel(request.X, request.Y, request.Rgb);
            appliedPixels.Add(new PixelPlacedEvent(request.X, request.Y, request.Rgb));
        }

        logger.LogInformation("Placed {Count} pixels by {ConnectionId}", appliedPixels.Count, Context.ConnectionId);

        await Clients.All.SendAsync("PixelsPlaced", appliedPixels);
    }

    public async Task PlaceStrokeSegments(IReadOnlyList<StrokeSegmentRequest> segments)
    {
        if (segments.Count == 0)
            return;

        foreach (var segment in segments)
        {
            ValidateColor(segment.Rgb);
            var brushSize = Math.Clamp(segment.BrushSize, 1, 8);
            var brushOffsets = CreateBrushOffsets(brushSize);

            foreach (var (x, y) in RasterizeSegment(segment))
            {
                foreach (var (offsetX, offsetY) in brushOffsets)
                {
                    var targetX = x + offsetX;
                    var targetY = y + offsetY;

                    if (targetX < 0 || targetX >= config.Width || targetY < 0 || targetY >= config.Height)
                        continue;

                    canvasState.SetPixel(targetX, targetY, segment.Rgb);
                }
            }
        }

        logger.LogInformation("Placed {Count} stroke segments by {ConnectionId}", segments.Count, Context.ConnectionId);

        await Clients.All.SendAsync("StrokeSegmentsPlaced", segments);
    }

    private void ValidateCoordinates(int x, int y)
    {
        if (x < 0 || x >= config.Width || y < 0 || y >= config.Height)
            throw new HubException("Invalid pixel coordinates");
    }

    private static void ValidateColor(Rgb rgb)
    {
        if (rgb.R < 0 || rgb.R > 255 || rgb.G < 0 || rgb.G > 255 || rgb.B < 0 || rgb.B > 255)
            throw new HubException("Invalid color values");
    }

    private IEnumerable<(int X, int Y)> RasterizeSegment(StrokeSegmentRequest segment)
    {
        return segment.Kind switch
        {
            "line" => RasterizeLine(segment.From, segment.To),
            "quadratic" when segment.Control is not null => RasterizeQuadratic(segment.From, segment.Control, segment.To),
            _ => throw new HubException("Invalid stroke segment")
        };
    }

    private IEnumerable<(int X, int Y)> RasterizeLine(GridPoint from, GridPoint to)
    {
        var start = ToGridCell(from);
        var end = ToGridCell(to);
        ValidateCoordinates(start.X, start.Y);
        ValidateCoordinates(end.X, end.Y);

        var x = start.X;
        var y = start.Y;
        var deltaX = Math.Abs(end.X - start.X);
        var deltaY = Math.Abs(end.Y - start.Y);
        var stepX = start.X < end.X ? 1 : -1;
        var stepY = start.Y < end.Y ? 1 : -1;
        var error = deltaX - deltaY;

        yield return (x, y);

        while (x != end.X || y != end.Y)
        {
            var doubledError = error * 2;

            if (doubledError > -deltaY)
            {
                error -= deltaY;
                x += stepX;
            }

            if (doubledError < deltaX)
            {
                error += deltaX;
                y += stepY;
            }

            ValidateCoordinates(x, y);
            yield return (x, y);
        }
    }

    private IEnumerable<(int X, int Y)> RasterizeQuadratic(GridPoint from, GridPoint control, GridPoint to)
    {
        var seen = new HashSet<(int X, int Y)>();
        var curveLength = Distance(from, control) + Distance(control, to);
        var steps = Math.Max(4, (int)Math.Ceiling(curveLength * 3));

        for (var i = 0; i <= steps; i++)
        {
            var t = (double)i / steps;
            var inverseT = 1 - t;
            var point = new GridPoint(
                inverseT * inverseT * from.X + 2 * inverseT * t * control.X + t * t * to.X,
                inverseT * inverseT * from.Y + 2 * inverseT * t * control.Y + t * t * to.Y
            );

            var cell = ToGridCell(point);
            ValidateCoordinates(cell.X, cell.Y);

            if (!seen.Add((cell.X, cell.Y)))
                continue;

            yield return (cell.X, cell.Y);
        }
    }

    private static GridCell ToGridCell(GridPoint point)
    {
        return new GridCell((int)Math.Floor(point.X), (int)Math.Floor(point.Y));
    }

    private static double Distance(GridPoint a, GridPoint b)
    {
        return Math.Sqrt(Math.Pow(b.X - a.X, 2) + Math.Pow(b.Y - a.Y, 2));
    }

    private static List<(int X, int Y)> CreateBrushOffsets(int brushSize)
    {
        if (brushSize <= 1)
            return new List<(int X, int Y)> { (0, 0) };

        var radius = brushSize - 1;
        var radiusSquared = radius * radius;
        var offsets = new List<(int X, int Y)>();

        for (var y = -radius; y <= radius; y++)
        {
            for (var x = -radius; x <= radius; x++)
            {
                if (x * x + y * y <= radiusSquared)
                    offsets.Add((x, y));
            }
        }

        return offsets;
    }
}

public record PixelPlacementRequest(int X, int Y, Rgb Rgb);
public record PixelPlacedEvent(int X, int Y, Rgb Rgb);
public record GridPoint(double X, double Y);
public record GridCell(int X, int Y);
public record StrokeSegmentRequest(
    [property: System.Text.Json.Serialization.JsonPropertyName("kind")] string Kind,
    [property: System.Text.Json.Serialization.JsonPropertyName("from")] GridPoint From,
    [property: System.Text.Json.Serialization.JsonPropertyName("to")] GridPoint To,
    [property: System.Text.Json.Serialization.JsonPropertyName("control")] GridPoint? Control,
    [property: System.Text.Json.Serialization.JsonPropertyName("rgb")] Rgb Rgb,
    [property: System.Text.Json.Serialization.JsonPropertyName("clientId")] string ClientId,
    [property: System.Text.Json.Serialization.JsonPropertyName("brushSize")] int BrushSize
);
