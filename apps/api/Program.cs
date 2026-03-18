using System.Runtime.InteropServices;
using pixels_site.Api.Canvas;
using pixels_site.Api.Hubs;
using pixels_site.Api.Services;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddSignalR();
builder.Services.AddSingleton<CanvasConfiguration>();
builder.Services.AddSingleton<CanvasStateService>();
builder.Services.AddSingleton<RateLimiter>();

var frontendOrigin = builder.Configuration["FrontendOrigin"];

builder.Services.AddCors(options =>
{
    options.AddPolicy("frontend", policy =>
    {
        if (string.IsNullOrWhiteSpace(frontendOrigin))
        {
            throw new InvalidOperationException("Missing required configuration: FrontendOrigin");
        }

        policy
            .WithOrigins(frontendOrigin)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();

    });
});

var app = builder.Build();

app.UseCors("frontend");

app.MapOpenApi();
app.MapScalarApiReference();

app.MapGet("/api/health", () => Results.Ok(new { ok = true }));
app.MapGet("/api/canvas/config", (CanvasConfiguration config) => Results.Ok(new { width = config.Width, height = config.Height }));
app.MapGet("/api/canvas/snapshot", (CanvasStateService canvasState) =>
{
    var snapshot = canvasState.GetSnapshot();
    return Results.Bytes(MemoryMarshal.AsBytes(snapshot.AsSpan()).ToArray(), "application/octet-stream");
});

app.MapHub<CanvasHub>("/hubs/canvas");

app.Run();
