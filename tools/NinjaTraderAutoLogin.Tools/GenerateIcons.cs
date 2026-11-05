using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

internal static class GenerateIcons
{
    private static GraphicsPath Rounded(float x, float y, float w, float h, float r)
    {
        var path = new GraphicsPath();
        float d = r * 2;
        path.AddArc(x, y, d, d, 180, 90);
        path.AddArc(x + w - d, y, d, d, 270, 90);
        path.AddArc(x + w - d, y + h - d, d, d, 0, 90);
        path.AddArc(x, y + h - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
    private static Bitmap Render(int size, bool settings)
    {
        var bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using (Graphics g = Graphics.FromImage(bitmap))
        {
            g.Clear(Color.Transparent);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.ScaleTransform(size / 256F, size / 256F);
            using (var path = Rounded(12, 12, 232, 232, 52))
            using (var brush = new LinearGradientBrush(new Point(20, 20), new Point(220, 240), settings ? Color.FromArgb(61, 61, 64) : Color.FromArgb(255, 66, 0), settings ? Color.FromArgb(27, 27, 27) : Color.FromArgb(220, 57, 0)))
            {
                g.FillPath(brush, path);
            }

            using (var pen = new Pen(settings ? Color.FromArgb(255, 102, 51) : Color.White, 16))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                pen.LineJoin = LineJoin.Round;
                if (settings)
                {
                    var gear = new List<PointF>();
                    for (int i = 0; i < 48; i++)
                    {
                        double angle = i * Math.PI / 24;
                        float radius = (i % 6 == 0 || i % 6 == 5) ? 58 : 73;
                        gear.Add(new PointF(128 + (float)Math.Cos(angle) * radius, 128 + (float)Math.Sin(angle) * radius));
                    }
                    g.DrawPolygon(pen, gear.ToArray());
                    g.DrawEllipse(pen, 103, 103, 50, 50);
                }
                else
                {
                    g.DrawLines(pen, new PointF[] { new PointF(137, 63), new PointF(190, 63), new PointF(190, 193), new PointF(137, 193) });
                    g.DrawLine(pen, 60, 128, 147, 128);
                    g.DrawLines(pen, new PointF[] { new PointF(116, 96), new PointF(148, 128), new PointF(116, 160) });
                }
            }
        }
        return bitmap;
    }
    private static void Save(string folder, string name, bool settings)
    {
        int[] sizes = { 16, 24, 32, 48, 64, 128, 256 };
        var images = new List<byte[]>();
        foreach (int size in sizes)
        {
            using (Bitmap bitmap = Render(size, settings))
            using (var stream = new MemoryStream())
            {
                bitmap.Save(stream, ImageFormat.Png);
                images.Add(stream.ToArray());
            }
        }

        using (var stream = File.Create(Path.Combine(folder, name + ".ico")))
        using (var writer = new BinaryWriter(stream))
        {
            writer.Write((ushort)0);
            writer.Write((ushort)1);
            writer.Write((ushort)sizes.Length);
            int offset = 6 + 16 * sizes.Length;
            for (int i = 0; i < sizes.Length; i++)
            {
                writer.Write((byte)(sizes[i] == 256 ? 0 : sizes[i]));
                writer.Write((byte)(sizes[i] == 256 ? 0 : sizes[i]));
                writer.Write((byte)0);
                writer.Write((byte)0);
                writer.Write((ushort)1);
                writer.Write((ushort)32);
                writer.Write(images[i].Length);
                writer.Write(offset);
                offset += images[i].Length;
            }
            foreach (byte[] image in images)
            {
                writer.Write(image);
            }
        }
        using (Bitmap preview = Render(256, settings))
        {
            preview.Save(Path.Combine(folder, name + ".png"), ImageFormat.Png);
        }
    }
    internal static int Run(string[] args)
    {
        if (args.Length != 1)
        {
            return 2;
        }

        Directory.CreateDirectory(args[0]);
        Save(args[0], "launch", false);
        Save(args[0], "settings", true);
        Console.WriteLine("Created launch/settings ICOs (16..256 px) from project vector geometry; no app UI rendered.");
        return 0;
    }
}
