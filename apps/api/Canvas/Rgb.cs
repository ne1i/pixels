using System.Runtime.InteropServices;

namespace pixels_site.Api.Canvas;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct Rgb(byte r, byte g, byte b)
{
    public byte R = r;
    public byte G = g;
    public byte B = b;
}
