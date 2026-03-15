var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddSignalR();

var app = builder.Build();

app.MapGet("/api/health", () => Results.Ok(new { ok = true }));
// app.MapHub<CanvasHub>("/hubs/canvas");
app.Run();
