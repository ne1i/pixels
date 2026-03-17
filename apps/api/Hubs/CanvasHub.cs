using Microsoft.AspNetCore.SignalR;
using pixels_site.Api.Canvas;

namespace pixels_site.Api.Hubs;

public class CanvasHub(CanvasStateService canvasState, CanvasConfiguration config, ILogger<CanvasHub> logger) : Hub
{
    public async Task PlacePixel(PixelPlacementRequest request)
    {
        logger.LogInformation("PlacePixel received: ({X}, {Y}) rgb({R}, {G}, {B})", request.X, request.Y, request.R, request.G, request.B);

        if (request.X < 0 || request.X >= config.Width || request.Y < 0 || request.Y >= config.Height)
            throw new HubException("Invalid pixel coordinates");

        if (request.R < 0 || request.R > 255 || request.G < 0 || request.G > 255 || request.B < 0 || request.B > 255)
            throw new HubException("Invalid color values");

        canvasState.SetPixel(request.X, request.Y, request.R, request.G, request.B);

        logger.LogInformation("Pixel placed at ({X}, {Y}) with color rgb({R}, {G}, {B}) by {ConnectionId}",
            request.X, request.Y, request.R, request.G, request.B, Context.ConnectionId);

        await Clients.All.SendAsync("PixelPlaced", new PixelPlacedEvent(request.X, request.Y, request.R, request.G, request.B));
    }
}

public record PixelPlacementRequest(int X, int Y, int R, int G, int B);
public record PixelPlacedEvent(int X, int Y, int R, int G, int B);
