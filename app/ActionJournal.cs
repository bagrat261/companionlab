using System;
using System.IO;
using System.Text.RegularExpressions;

namespace CapriDesktop
{
    internal static class ActionJournal
    {
        internal static string Path;
        private static readonly object gate = new object();

        internal static void Record(string tool, string phase)
        {
            if (Path == null) return;
            tool = Regex.Replace(tool ?? "unknown", "[^a-zA-Z0-9_-]", "_");
            phase = Regex.Replace(phase ?? "unknown", "[^a-zA-Z0-9_:-]", "_");
            lock (gate)
            {
                try
                {
                    Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path));
                    if (File.Exists(Path) && new FileInfo(Path).Length > 1024 * 1024)
                    {
                        if (File.Exists(Path + ".previous")) File.Delete(Path + ".previous");
                        File.Move(Path, Path + ".previous");
                    }
                    File.AppendAllText(Path, DateTime.UtcNow.ToString("o") + " " + tool + " " + phase + Environment.NewLine);
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }
    }
}
