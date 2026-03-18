using Microsoft.AspNetCore.SignalR;
using pixels_site.Api.Canvas;

namespace pixels_site.Api.Hubs;

public class CanvasHub(CanvasStateService canvasState, CanvasConfiguration config, ILogger<CanvasHub> logger) : Hub
{
    public async Task PlacePixel(PixelPlacementRequest request)
    {
        logger.LogInformation("PlacePixel received: ({X}, {Y}) rgb({R}, {G}, {B})", request.X, request.Y, request.Rgb.R, request.Rgb.G, request.Rgb.B);

        if (request.X < 0 || request.X >= config.Width || request.Y < 0 || request.Y >= config.Height)
            throw new HubException("Invalid pixel coordinates");

        if (request.Rgb.R < 0 || request.Rgb.R > 255 || request.Rgb.G < 0 || request.Rgb.G > 255 || request.Rgb.B < 0 || request.Rgb.B > 255)
            throw new HubException("Invalid color values");

        canvasState.SetPixel(request.X, request.Y, request.Rgb.R, request.Rgb.G, request.Rgb.B);

        logger.LogInformation("Pixel placed at ({X}, {Y}) with color rgb({RGB}) by {ConnectionId}",
            request.X, request.Y, request.Rgb, Context.ConnectionId);

        await Clients.All.SendAsync("PixelPlaced", new PixelPlacedEvent(request.X, request.Y, request.Rgb));
    }
}

public record PixelPlacementRequest(int X, int Y, Rgb Rgb);
public record PixelPlacedEvent(int X, int Y, Rgb Rgb);
