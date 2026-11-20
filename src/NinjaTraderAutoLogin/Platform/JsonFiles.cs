using System;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Threading;

namespace NinjaTraderAutoLogin
{
    // A record can be read while another process replaces it: the app reads the run record for
    // its progress window while the worker writes it. Readers here share delete, so a replace
    // goes ahead while they read; another program, such as a virus scanner, may still hold a
    // file for a moment, so a replace that fails is retried briefly before it gives up.
    internal static class JsonFiles
    {
        internal const int ReplaceAttempts = 10;
        internal static readonly TimeSpan ReplacePause = TimeSpan.FromMilliseconds(50);

        public static T Read<T>(string path)
        {
            using (Stream stream = OpenRead(path))
            {
                return (T)new DataContractJsonSerializer(typeof(T)).ReadObject(stream);
            }
        }

        internal static Stream OpenRead(string path)
        {
            return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        }
        public static void Write<T>(string path, T value)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    new DataContractJsonSerializer(typeof(T)).WriteObject(stream, value);
                    stream.Flush(true);
                }
                for (int attempt = 1; ; attempt++)
                {
                    try
                    {
                        if (File.Exists(path))
                        {
                            File.Replace(temporary, path, null);
                        }
                        else
                        {
                            File.Move(temporary, path);
                        }

                        break;
                    }
                    catch (Exception error) when ((error is IOException || error is UnauthorizedAccessException) && attempt < ReplaceAttempts && File.Exists(temporary))
                    {
                        Thread.Sleep(ReplacePause);
                    }
                }
            }
            finally
            {
                if (File.Exists(temporary))
                {
                    File.Delete(temporary);
                }
            }
        }
    }
}
