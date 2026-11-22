using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace MiniCpp {
    internal static class DesktopIntegration {
        internal const string ProgId="MiniCpp.Portable.Source.cpp";
        internal const string ApplicationName="MiniCpp.Portable";
        internal const string Capabilities=@"Software\MiniCpp\Portable\Capabilities";
        internal const string ShortcutName="Mini C++ (免安装版).lnk";
        internal static string Executable { get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"MiniCpp.exe"); } }

        [DllImport("shell32.dll")] static extern void SHChangeNotify(uint action,uint flags,IntPtr item1,IntPtr item2);

        internal static void CreateDesktopShortcut(){
            string desktop=Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            if(string.IsNullOrEmpty(desktop))throw new IOException("无法找到当前用户的桌面目录。");
            Directory.CreateDirectory(desktop);
            string target=Path.Combine(desktop,ShortcutName),temp=Path.Combine(desktop,"MiniCpp-"+Guid.NewGuid().ToString("N")+".lnk");
            object shell=null,shortcut=null;
            try {
                var type=Type.GetTypeFromProgID("WScript.Shell",true);
                shell=Activator.CreateInstance(type);
                shortcut=type.InvokeMember("CreateShortcut",BindingFlags.InvokeMethod,null,shell,new object[]{temp});
                var linkType=shortcut.GetType();
                linkType.InvokeMember("TargetPath",BindingFlags.SetProperty,null,shortcut,new object[]{Executable});
                linkType.InvokeMember("WorkingDirectory",BindingFlags.SetProperty,null,shortcut,new object[]{AppDomain.CurrentDomain.BaseDirectory});
                linkType.InvokeMember("IconLocation",BindingFlags.SetProperty,null,shortcut,new object[]{Path.Combine(Ui.Assets,"mini-cpp.ico")+",0"});
                linkType.InvokeMember("Description",BindingFlags.SetProperty,null,shortcut,new object[]{"Mini C++ 免安装版"});
                linkType.InvokeMember("Save",BindingFlags.InvokeMethod,null,shortcut,null);
                if(File.Exists(target))File.Replace(temp,target,null);else File.Move(temp,target);
            } finally {
                if(shortcut!=null)Marshal.FinalReleaseComObject(shortcut);
                if(shell!=null)Marshal.FinalReleaseComObject(shell);
                if(File.Exists(temp))File.Delete(temp);
            }
        }

        internal static void RegisterCpp(){
            // A separate ProgID keeps this executable address distinct from the installed edition.
            using(var key=Registry.CurrentUser.CreateSubKey(@"Software\Classes\"+ProgId)){
                key.SetValue("","C++ 源文件 (Mini C++ 免安装版)");
                using(var icon=key.CreateSubKey("DefaultIcon"))icon.SetValue("",Native.Quote(Path.Combine(Ui.Assets,"cpp-file.ico"))+",0");
                using(var command=key.CreateSubKey(@"shell\open\command"))command.SetValue("",Native.Quote(Executable)+" \"%1\"");
            }
            using(var key=Registry.CurrentUser.CreateSubKey(@"Software\Classes\.cpp\OpenWithProgids"))key.SetValue(ProgId,"");
            using(var key=Registry.CurrentUser.CreateSubKey(Capabilities)){
                key.SetValue("ApplicationName","Mini C++ (免安装版)");
                key.SetValue("ApplicationDescription","C++ 单文件竞赛编辑器");
                key.SetValue("ApplicationIcon",Native.Quote(Executable)+",0");
                using(var associations=key.CreateSubKey("FileAssociations"))associations.SetValue(".cpp",ProgId);
            }
            using(var key=Registry.CurrentUser.CreateSubKey(@"Software\RegisteredApplications"))key.SetValue(ApplicationName,Capabilities);
            SHChangeNotify(0x08000000,0,IntPtr.Zero,IntPtr.Zero);
        }

        internal static void OpenDefaultApps(){
            try { Process.Start(new ProcessStartInfo("ms-settings:defaultapps?registeredAppUser="+ApplicationName){UseShellExecute=true}); }
            catch(System.ComponentModel.Win32Exception){Process.Start(new ProcessStartInfo("ms-settings:defaultapps"){UseShellExecute=true});}
        }
    }
}
