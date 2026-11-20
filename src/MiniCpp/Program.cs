using System;
using System.Reflection;
using System.Windows.Forms;
[assembly:AssemblyTitle("Mini C++")]
[assembly:AssemblyProduct("Mini C++")]
[assembly:AssemblyVersion("0.1.0.0")]
[assembly:AssemblyFileVersion("0.1.0.0")]
namespace MiniCpp {
    internal static class Program {
        [STAThread] static void Main(string[] args){
            Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
            Application.ThreadException+=(s,e)=>MessageBox.Show(e.Exception.Message,"Mini C++",MessageBoxButtons.OK,MessageBoxIcon.Warning);
            try{Application.Run(new MainForm(args));}catch(Exception ex){MessageBox.Show(ex.ToString(),"Mini C++ 无法启动",MessageBoxButtons.OK,MessageBoxIcon.Error);}
        }
    }
}
