using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace MiniCpp {
    internal static class Native {
        public static string Quote(string value) {
            var b = new StringBuilder("\""); int slashes = 0;
            foreach (char c in value) {
                if (c == '\\') { slashes++; continue; }
                if (c == '"') b.Append('\\', slashes * 2 + 1); else b.Append('\\', slashes);
                b.Append(c); slashes = 0;
            }
            return b.Append('\\', slashes * 2).Append('"').ToString();
        }
        public static string Args(params string[] args) { return string.Join(" ", args.Select(Quote)); }
        public static ProcessStartInfo StartInfo(string exe, string args, string cwd, string bin) {
            var p = new ProcessStartInfo(exe, args) { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = cwd };
            if (!string.IsNullOrEmpty(bin)) p.EnvironmentVariables["PATH"] = bin + ";" + (Environment.GetEnvironmentVariable("PATH") ?? "");
            return p;
        }
        public static string DataDir { get { string custom=Environment.GetEnvironmentVariable("MINICPP_HOME");return !string.IsNullOrWhiteSpace(custom)&&Path.IsPathRooted(custom)?Path.GetFullPath(custom):Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MiniCpp"); } }
        public static void Kill(Process p) { try { if (p != null && !p.HasExited) p.Kill(); } catch (InvalidOperationException) {} catch (System.ComponentModel.Win32Exception) {} }
        public static readonly Encoding Utf8 = new UTF8Encoding(false);
    }

    // Close the job to stop only processes created for this build/run, including compiler descendants.
    internal sealed class ChildJob : IDisposable {
        [StructLayout(LayoutKind.Sequential)] struct Basic { public long perProcess, perJob; public uint flags; public UIntPtr min, max; public uint limit; public UIntPtr affinity; public uint priority, scheduling; }
        [StructLayout(LayoutKind.Sequential)] struct Io { public ulong a,b,c,d,e,f; }
        [StructLayout(LayoutKind.Sequential)] struct Extended { public Basic basic; public Io io; public UIntPtr a,b,c,d; }
        [DllImport("kernel32.dll", CharSet=CharSet.Unicode)] static extern SafeFileHandle CreateJobObject(IntPtr attrs, string name);
        [DllImport("kernel32.dll")] static extern bool SetInformationJobObject(SafeFileHandle h, int cls, ref Extended info, int cb);
        [DllImport("kernel32.dll")] static extern bool AssignProcessToJobObject(SafeFileHandle h, IntPtr proc);
        SafeFileHandle handle;
        public ChildJob() { handle = CreateJobObject(IntPtr.Zero, null); var info = new Extended(); info.basic.flags = 0x2000; if (!handle.IsInvalid && !SetInformationJobObject(handle,9,ref info,Marshal.SizeOf(info))) handle.Dispose(); }
        public void Add(Process p) { try { if (!handle.IsInvalid && !handle.IsClosed) AssignProcessToJobObject(handle,p.Handle); } catch(InvalidOperationException) {} }
        public void Dispose() { if (handle != null) handle.Dispose(); }
    }
}
