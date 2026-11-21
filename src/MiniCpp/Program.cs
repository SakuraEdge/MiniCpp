using System;
using System.Reflection;
using System.Windows.Forms;
[assembly:AssemblyTitle("Mini C++")]
[assembly:AssemblyProduct("Mini C++")]
[assembly:AssemblyVersion("1.0.1.0")]
[assembly:AssemblyFileVersion("1.0.1.0")]
[assembly:AssemblyInformationalVersion("1.0.1")]
[assembly:System.Runtime.Versioning.TargetFramework(".NETFramework,Version=v4.8")]
namespace MiniCpp {
    internal static class Program {
        internal const string ReleaseTag="v1.0.1",DisplayVersion="Rev v1.0.1";
        [STAThread] static void Main(string[] args){
            Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
            Application.ThreadException+=(s,e)=>MessageBox.Show(e.Exception.Message,"Mini C++",MessageBoxButtons.OK,MessageBoxIcon.Warning);
            try{Application.Run(new MainForm(args));}catch(Exception ex){MessageBox.Show(ex.ToString(),"Mini C++ 无法启动",MessageBoxButtons.OK,MessageBoxIcon.Error);}
        }
    }
}
