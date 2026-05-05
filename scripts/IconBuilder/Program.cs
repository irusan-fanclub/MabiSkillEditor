using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

internal static class Program
{
    // Lucide wand path data (viewBox 0 0 24 24, stroked)
    private static readonly string[] PathData =
    {
        "M 15 4 V 2",
        "M 15 16 v -2",
        "M 8 9 h 2",
        "M 20 9 h 2",
        "M 17.8 11.8 L 19 13",
        "M 15 9 h .01",
        "M 17.8 6.2 L 19 5",
        "M 3 21 L 12 12",
        "M 12.2 6.2 L 11 5",
    };

    [STAThread]
    private static int Main(string[] args)
    {
        var outPath = args.Length > 0 ? args[0] : "app.ico";
        var hex     = args.Length > 1 ? args[1] : "#89b4fa";

        var color = (Color)ColorConverter.ConvertFromString(hex)!;
        var brush = new SolidColorBrush(color); brush.Freeze();
        var pen = new Pen(brush, 2.0)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap   = PenLineCap.Round,
            LineJoin     = PenLineJoin.Round,
        };
        pen.Freeze();

        var dg = new DrawingGroup();
        foreach (var p in PathData)
            dg.Children.Add(new GeometryDrawing(null, pen, Geometry.Parse(p)));
        dg.Freeze();

        int[] sizes = { 16, 24, 32, 48, 64, 128, 256 };
        var pngs = new List<(int Size, byte[] Data)>();
        foreach (var size in sizes)
        {
            var visual = new DrawingVisual();
            using (var ctx = visual.RenderOpen())
            {
                ctx.PushTransform(new ScaleTransform(size / 24.0, size / 24.0));
                ctx.DrawDrawing(dg);
                ctx.Pop();
            }
            var rtb = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(visual);
            var enc = new PngBitmapEncoder();
            enc.Frames.Add(BitmapFrame.Create(rtb));
            using var ms = new MemoryStream();
            enc.Save(ms);
            pngs.Add((size, ms.ToArray()));
        }

        // Write ICO (PNG-embedded)
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outPath))!);
        using var fs = File.Open(outPath, FileMode.Create);
        using var bw = new BinaryWriter(fs);
        bw.Write((ushort)0);                  // reserved
        bw.Write((ushort)1);                  // type = 1 (icon)
        bw.Write((ushort)pngs.Count);

        int offset = 6 + pngs.Count * 16;
        foreach (var (size, data) in pngs)
        {
            bw.Write((byte)(size >= 256 ? 0 : size));   // width  (0 = 256)
            bw.Write((byte)(size >= 256 ? 0 : size));   // height
            bw.Write((byte)0);                           // colors
            bw.Write((byte)0);                           // reserved
            bw.Write((ushort)1);                         // planes
            bw.Write((ushort)32);                        // bitcount
            bw.Write((uint)data.Length);
            bw.Write((uint)offset);
            offset += data.Length;
        }
        foreach (var (_, data) in pngs)
            bw.Write(data);

        Console.WriteLine($"Wrote {Path.GetFullPath(outPath)} with {pngs.Count} frames ({string.Join(",", sizes)}) color={hex}");
        return 0;
    }
}
