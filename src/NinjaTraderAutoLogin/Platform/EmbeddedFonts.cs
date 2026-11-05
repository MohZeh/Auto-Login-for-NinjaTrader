using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Text;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;

namespace NinjaTraderAutoLogin
{
    // Both GDI+ FontFamily and native GDI text controls receive the EXE's own bytes.
    // No installed-family lookup, disk extraction, font directory, or network fallback.
    internal static class UiFonts
    {
        public const string RegularResource = "NinjaTraderAutoLogin.Fonts.Vazir.Regular.ttf", BoldResource = "NinjaTraderAutoLogin.Fonts.Vazir.Bold.ttf";
        private static readonly Dictionary<string, Font> cache = new Dictionary<string, Font>();
        private static readonly List<IntPtr> buffers = new List<IntPtr>(), registrations = new List<IntPtr>();
        private static PrivateFontCollection collection;
        private static readonly object sync = new object();
        [DllImport("gdi32.dll")] private static extern IntPtr AddFontMemResourceEx(IntPtr bytes, uint length, IntPtr reserved, out uint count);
        [DllImport("gdi32.dll")] private static extern bool RemoveFontMemResourceEx(IntPtr registration);
        public static Font Get(string language, float points, FontStyle style)
        {
            lock (sync)
            {
                string key = language + ":" + points.ToString("R", CultureInfo.InvariantCulture) + ":" + style;
                Font font;
                if (cache.TryGetValue(key, out font))
                {
                    return font;
                }

                if (language == "fa")
                {
                    EnsureEmbedded();
                    font = new Font(collection.Families[0], points, style, GraphicsUnit.Point);
                }
                else
                {
                    font = new Font("Segoe UI", points, style, GraphicsUnit.Point);
                }

                cache.Add(key, font);
                return font;
            }
        }
        private static void EnsureEmbedded()
        {
            if (collection != null)
            {
                return;
            }

            collection = new PrivateFontCollection();
            try
            {
                Load(RegularResource);
                Load(BoldResource);
                if (collection.Families.Length != 1 || collection.Families[0].Name != "Vazir" ||
                    !collection.Families[0].IsStyleAvailable(FontStyle.Regular) || !collection.Families[0].IsStyleAvailable(FontStyle.Bold))
                {
                    throw new InvalidDataException("EmbeddedVazirFamilyInvalid");
                }
            }
            catch { Release(); throw; }
        }
        private static void Load(string name)
        {
            byte[] bytes;
            using (Stream stream = typeof(UiFonts).Assembly.GetManifestResourceStream(name))
            {
                if (stream == null || stream.Length < 1000 || stream.Length > 1000000)
                {
                    throw new InvalidDataException("EmbeddedFontMissing");
                }

                using (var memory = new MemoryStream())
                {
                    stream.CopyTo(memory);
                    bytes = memory.ToArray();
                }
            }
            IntPtr buffer = Marshal.AllocHGlobal(bytes.Length);
            buffers.Add(buffer);
            Marshal.Copy(bytes, 0, buffer, bytes.Length);
            collection.AddMemoryFont(buffer, bytes.Length);
            uint count;
            IntPtr registration = AddFontMemResourceEx(buffer, (uint)bytes.Length, IntPtr.Zero, out count);
            if (registration == IntPtr.Zero || count == 0)
            {
                throw new InvalidDataException("EmbeddedFontRegistrationFailed");
            }

            registrations.Add(registration);
        }
        public static void Release()
        {
            lock (sync)
            {
                foreach (Font font in cache.Values)
                {
                    font.Dispose();
                }

                cache.Clear();
                foreach (IntPtr registration in registrations)
                {
                    RemoveFontMemResourceEx(registration);
                }

                registrations.Clear();
                if (collection != null)
                {
                    collection.Dispose();
                    collection = null;
                }
                foreach (IntPtr buffer in buffers)
                {
                    Marshal.FreeHGlobal(buffer);
                }

                buffers.Clear();
            }
        }
    }
}
