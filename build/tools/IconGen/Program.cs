// Generates Assets/NanoVault.ico procedurally: a rounded purple-gradient
// square with a white double music note, rendered at 256px and box-filtered
// down to 48/32/16. Uses BMP-in-ICO entries so no image libraries are needed.
// Run: dotnet run --project build/tools/IconGen -- <output.ico>

using System.Buffers.Binary;

var output = args.Length > 0 ? args[0] : "NanoVault.ico";

var master = Render(256);
int[] sizes = [256, 48, 32, 16];
var images = sizes.Select(s => s == 256 ? master : Downsample(master, 256, s)).ToArray();

WriteIco(output, sizes, images);
Console.WriteLine($"Wrote {output} ({new FileInfo(output).Length} bytes)");
return;

// ------------------------------------------------------------ rendering

static uint[] Render(int size)
{
    var pixels = new uint[size * size];
    double s = size;
    var radius = s * 0.22;

    for (var y = 0; y < size; y++)
    {
        for (var x = 0; x < size; x++)
        {
            // Rounded-rect coverage with 2px soft edge.
            var alpha = RoundedRectCoverage(x + 0.5, y + 0.5, s * 0.04, s * 0.04, s * 0.92, s * 0.92, radius);
            if (alpha <= 0)
            {
                continue;
            }

            // Vertical gradient: #7B68E0 → #4E3BB8, slight diagonal shift.
            var t = Math.Clamp((y + x * 0.25) / (s * 1.25), 0, 1);
            var r = Lerp(0x7B, 0x4E, t);
            var g = Lerp(0x68, 0x3B, t);
            var b = Lerp(0xE0, 0xB8, t);

            pixels[y * size + x] = Pack(r, g, b, (byte)(alpha * 255));
        }
    }

    // White double music note (beamed quavers), drawn with filled shapes.
    // Coordinates in the 0..1 unit square of the icon.
    DrawNote(pixels, size);
    return pixels;
}

static void DrawNote(uint[] pixels, int size)
{
    double s = size;

    // Note heads: two ellipses.
    FillEllipse(pixels, size, s * 0.36, s * 0.66, s * 0.085, s * 0.062, -0.35);
    FillEllipse(pixels, size, s * 0.64, s * 0.62, s * 0.085, s * 0.062, -0.35);

    // Stems: vertical bars from each head up to the beam.
    FillRect(pixels, size, s * 0.415, s * 0.30, s * 0.032, s * 0.37);
    FillRect(pixels, size, s * 0.695, s * 0.26, s * 0.032, s * 0.37);

    // Beam: slanted thick bar joining the stems.
    FillSlantedBar(pixels, size, s * 0.415, s * 0.335, s * 0.727, s * 0.295, s * 0.075);
}

static double RoundedRectCoverage(double px, double py, double x, double y, double w, double h, double r)
{
    // Signed distance to the rounded rectangle, soft 1.5px anti-aliased edge.
    var cx = Math.Clamp(px, x + r, x + w - r);
    var cy = Math.Clamp(py, y + r, y + h - r);
    var dx = px - cx;
    var dy = py - cy;
    var distance = Math.Sqrt(dx * dx + dy * dy);
    return Math.Clamp((r - distance) / 1.5 + 1.0, 0, 1);
}

static void FillEllipse(uint[] pixels, int size, double cx, double cy, double rx, double ry, double rotation)
{
    var cos = Math.Cos(rotation);
    var sin = Math.Sin(rotation);

    for (var y = 0; y < size; y++)
    {
        for (var x = 0; x < size; x++)
        {
            var dx = x + 0.5 - cx;
            var dy = y + 0.5 - cy;
            var ex = (dx * cos - dy * sin) / rx;
            var ey = (dx * sin + dy * cos) / ry;
            var d = ex * ex + ey * ey;
            if (d <= 1.0)
            {
                BlendWhite(pixels, size, x, y, 1.0);
            }
            else if (d <= 1.25)
            {
                BlendWhite(pixels, size, x, y, (1.25 - d) / 0.25);
            }
        }
    }
}

static void FillRect(uint[] pixels, int size, double x, double y, double w, double h)
{
    for (var py = (int)y; py < y + h && py < size; py++)
    {
        for (var px = (int)x; px < x + w && px < size; px++)
        {
            if (px >= 0 && py >= 0)
            {
                BlendWhite(pixels, size, px, py, 1.0);
            }
        }
    }
}

static void FillSlantedBar(uint[] pixels, int size, double x1, double y1, double x2, double y2, double thickness)
{
    var steps = (int)(Math.Abs(x2 - x1) + 1);
    for (var i = 0; i <= steps; i++)
    {
        var t = (double)i / steps;
        var x = x1 + (x2 - x1) * t;
        var y = y1 + (y2 - y1) * t;
        FillRect(pixels, size, x, y, 1.5, thickness);
    }
}

static void BlendWhite(uint[] pixels, int size, int x, int y, double intensity)
{
    var index = y * size + x;
    var existing = pixels[index];
    var alpha = (byte)(existing >> 24);
    if (alpha == 0)
    {
        return; // Outside the rounded square: keep transparent.
    }

    var r = (byte)(existing >> 16);
    var g = (byte)(existing >> 8);
    var b = (byte)existing;

    var nr = Lerp(r, 255, intensity);
    var ng = Lerp(g, 255, intensity);
    var nb = Lerp(b, 255, intensity);
    pixels[index] = Pack(nr, ng, nb, alpha);
}

static byte Lerp(byte from, byte to, double t) => (byte)Math.Clamp(from + (to - from) * t, 0, 255);

static uint Pack(byte r, byte g, byte b, byte a) => (uint)(a << 24 | r << 16 | g << 8 | b);

static uint[] Downsample(uint[] source, int sourceSize, int targetSize)
{
    var target = new uint[targetSize * targetSize];
    var scale = sourceSize / targetSize;

    for (var y = 0; y < targetSize; y++)
    {
        for (var x = 0; x < targetSize; x++)
        {
            double r = 0, g = 0, b = 0, a = 0;
            for (var sy = 0; sy < scale; sy++)
            {
                for (var sx = 0; sx < scale; sx++)
                {
                    var pixel = source[(y * scale + sy) * sourceSize + x * scale + sx];
                    var pa = (pixel >> 24) & 0xFF;
                    a += pa;
                    r += ((pixel >> 16) & 0xFF) * pa;
                    g += ((pixel >> 8) & 0xFF) * pa;
                    b += (pixel & 0xFF) * pa;
                }
            }

            var count = (double)(scale * scale);
            var outA = a / count;
            if (a > 0)
            {
                target[y * targetSize + x] = Pack(
                    (byte)Math.Clamp(r / a, 0, 255),
                    (byte)Math.Clamp(g / a, 0, 255),
                    (byte)Math.Clamp(b / a, 0, 255),
                    (byte)Math.Clamp(outA, 0, 255));
            }
        }
    }

    return target;
}

// ------------------------------------------------------------ ICO output

static void WriteIco(string path, int[] sizes, uint[][] images)
{
    using var stream = File.Create(path);
    using var writer = new BinaryWriter(stream);

    // ICONDIR
    writer.Write((ushort)0);
    writer.Write((ushort)1);
    writer.Write((ushort)sizes.Length);

    var imageData = new byte[sizes.Length][];
    for (var i = 0; i < sizes.Length; i++)
    {
        imageData[i] = EncodeBmp(images[i], sizes[i]);
    }

    var offset = 6 + 16 * sizes.Length;
    for (var i = 0; i < sizes.Length; i++)
    {
        var size = sizes[i];
        writer.Write((byte)(size == 256 ? 0 : size));
        writer.Write((byte)(size == 256 ? 0 : size));
        writer.Write((byte)0);
        writer.Write((byte)0);
        writer.Write((ushort)1);
        writer.Write((ushort)32);
        writer.Write(imageData[i].Length);
        writer.Write(offset);
        offset += imageData[i].Length;
    }

    foreach (var data in imageData)
    {
        writer.Write(data);
    }
}

static byte[] EncodeBmp(uint[] pixels, int size)
{
    var maskStride = (size / 8 + 3) & ~3;
    var data = new byte[40 + size * size * 4 + maskStride * size];
    var span = data.AsSpan();

    BinaryPrimitives.WriteInt32LittleEndian(span[0..], 40);           // biSize
    BinaryPrimitives.WriteInt32LittleEndian(span[4..], size);         // biWidth
    BinaryPrimitives.WriteInt32LittleEndian(span[8..], size * 2);     // biHeight (XOR + AND)
    BinaryPrimitives.WriteInt16LittleEndian(span[12..], 1);           // biPlanes
    BinaryPrimitives.WriteInt16LittleEndian(span[14..], 32);          // biBitCount
    BinaryPrimitives.WriteInt32LittleEndian(span[20..], size * size * 4 + maskStride * size);

    // XOR data: BGRA rows, bottom-up.
    var index = 40;
    for (var y = size - 1; y >= 0; y--)
    {
        for (var x = 0; x < size; x++)
        {
            var pixel = pixels[y * size + x];
            data[index++] = (byte)pixel;          // B
            data[index++] = (byte)(pixel >> 8);   // G
            data[index++] = (byte)(pixel >> 16);  // R
            data[index++] = (byte)(pixel >> 24);  // A
        }
    }

    // AND mask: all zero (alpha channel drives transparency).
    return data;
}
