using Microsoft.AspNetCore.SignalR;
using Microsoft.Net.Http.Headers;

namespace pixels_site.Api.Canvas;

public struct Rgb
{
    public byte R { get; set; }
    public byte G { get; set; }
    public byte B { get; set; }
}
