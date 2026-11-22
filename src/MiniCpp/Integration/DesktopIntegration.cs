using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using Microsoft.Win32;

namespace MiniCpp {
    internal static class DesktopIntegration {
        internal const string ProgId="MiniCpp.Portable.Source.cpp";
        internal const string ApplicationName="MiniCpp.Portable";
        internal const string Capabilities=@"Software\MiniCpp\Portable\Capabilities";
        internal const string ShortcutName="Mini C++ (免安装版).lnk";
        internal static string Executable { get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"MiniCpp.exe"); } }

        [DllImport("shell32.dll")] static extern void SHChangeNotify(uint action,uint flags,IntPtr item1,IntPtr item2);

        [ComImport,Guid("000214F9-0000-0000-C000-000000000046"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IShellLinkW {
            void GetPath([Out,MarshalAs(UnmanagedType.LPWStr)] StringBuilder path,int length,IntPtr findData,uint flags);
            void GetIDList(out IntPtr list);
            void SetIDList(IntPtr list);
            void GetDescription([Out,MarshalAs(UnmanagedType.LPWStr)] StringBuilder description,int length);
            void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string description);
            void GetWorkingDirectory([Out,MarshalAs(UnmanagedType.LPWStr)] StringBuilder directory,int length);
            void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string directory);
            void GetArguments([Out,MarshalAs(UnmanagedType.LPWStr)] StringBuilder arguments,int length);
            void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string arguments);
            void GetHotkey(out short hotkey);
            void SetHotkey(short hotkey);
            void GetShowCmd(out int command);
            void SetShowCmd(int command);
            void GetIconLocation([Out,MarshalAs(UnmanagedType.LPWStr)] StringBuilder path,int length,out int index);
            void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string path,int index);
            void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string path,uint reserved);
            void Resolve(IntPtr window,uint flags);
            void SetPath([MarshalAs(UnmanagedType.LPWStr)] string path);
        }

        internal static void CreateDesktopShortcut(){
            string desktop=Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            if(string.IsNullOrEmpty(desktop))throw new IOException("无法找到当前用户的桌面目录。");
            Directory.CreateDirectory(desktop);
            string target=Path.Combine(desktop,ShortcutName),temp=Path.Combine(desktop,"MiniCpp-"+Guid.NewGuid().ToString("N")+".lnk");
            object shortcut=null;
            try {
                // Use the Unicode Shell interface for paths outside the Windows ANSI code page.
                shortcut=Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("00021401-0000-0000-C000-000000000046"),true));
                var link=(IShellLinkW)shortcut;
                link.SetPath(Executable);
                link.SetWorkingDirectory(AppDomain.CurrentDomain.BaseDirectory);
                link.SetIconLocation(Path.Combine(Ui.Assets,"mini-cpp.ico"),0);
                link.SetDescription("Mini C++ 免安装版");
                ((IPersistFile)shortcut).Save(temp,true);
                if(File.Exists(target))File.Replace(temp,target,null);else File.Move(temp,target);
            } finally {
                if(shortcut!=null)Marshal.FinalReleaseComObject(shortcut);
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
