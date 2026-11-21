using System;
using System.IO;
using System.Text;

namespace MiniCpp {
    // A session's manifest is renamed into place only after the three text streams are closed.
    internal static class RunFiles {
        internal static readonly Encoding TextEncoding = new UnicodeEncoding(false, false);
        internal const string Input = "stdin", Output = "stdout", Error = "stderr";
        internal static string StreamPath(string manifest, string channel) {
            if (channel != Input && channel != Output && channel != Error) throw new ArgumentException("Invalid stream");
            return manifest + "." + channel;
        }
        internal static void Remove(string manifest) {
            foreach (string suffix in new[]{"", ".part", "."+Input, "."+Output, "."+Error}) {
                try { File.Delete(manifest + suffix); } catch (IOException) {} catch (UnauthorizedAccessException) {}
            }
        }
    }
}
