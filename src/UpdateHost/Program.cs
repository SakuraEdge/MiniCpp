using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

[assembly:System.Runtime.Versioning.TargetFramework(".NETFramework,Version=v4.8")]

namespace MiniCpp {
    internal static class UpdateHost {
        [STAThread] static int Main(string[] args){
            Application.EnableVisualStyles();string target=null,work=AppDomain.CurrentDomain.BaseDirectory;
            try{
                if(args.Length!=1)throw new ArgumentException("更新请求缺失。");
                var request=UpdatePackage.ReadXml(args[0]);target=Path.GetFullPath((string)request.Attribute("target")).TrimEnd('\\');
                if(target==Path.GetPathRoot(target).TrimEnd('\\')||!File.Exists(Path.Combine(target,"MiniCpp.exe")))throw new IOException("无法确认要更新的程序目录。");
                string mutexName;
                using(var hash=SHA256.Create())mutexName=@"Local\MiniCpp.Update."+BitConverter.ToString(hash.ComputeHash(Native.Utf8.GetBytes(target.ToUpperInvariant()))).Replace("-","");
                using(var mutex=new Mutex(false,mutexName)){
                    try{if(!mutex.WaitOne(0))throw new IOException("此目录已有更新任务。");}catch(AbandonedMutexException){}
                    try{
                        int pid=(int)request.Attribute("pid");long started=(long)request.Attribute("started");
                        try{using(var parent=Process.GetProcessById(pid)){if(parent.StartTime.ToUniversalTime().Ticks==started&&!parent.WaitForExit(120000))throw new IOException("编辑器仍在运行，更新已取消。");}}catch(ArgumentException){}
                        foreach(var process in Process.GetProcessesByName("MiniCpp"))using(process){
                            try{if(string.Equals(process.MainModule.FileName,Path.Combine(target,"MiniCpp.exe"),StringComparison.OrdinalIgnoreCase))throw new IOException("请关闭同一目录中的其他 Mini C++ 窗口后重试。");}
                            catch(System.ComponentModel.Win32Exception){}
                        }
                        if((string)request.Attribute("recover")=="true")UpdateTransaction.Recover(target);
                        else{
                            string version=(string)request.Attribute("version"),zip=Path.Combine(work,"update.zip"),stage=Path.Combine(work,"apply");
                            if(UpdatePackage.HashFile(zip)!=(string)request.Attribute("sha256"))throw new IOException("下载文件校验失败。");
                            UpdatePackage.Extract(zip,stage,version);UpdateTransaction.Apply(stage,target,version);
                            UpdateInstallRecord(target,version);
                        }
                    }finally{mutex.ReleaseMutex();}
                }
                File.WriteAllText(Path.Combine(work,"result.txt"),"success",Native.Utf8);
                ClearDownload(work);
                string[] sources=request.Elements("Source").Select(x=>x.Value).Where(File.Exists).ToArray();
                Process.Start(new ProcessStartInfo(Path.Combine(target,"MiniCpp.exe"),Native.Args(sources)){UseShellExecute=true,WorkingDirectory=target});
                return 0;
            }catch(Exception ex){
                try{File.WriteAllText(Path.Combine(work,"result.txt"),ex.ToString(),Native.Utf8);}catch(IOException){}
                MessageBox.Show("更新未完成。\n"+ex.Message+"\n\n诊断记录："+Path.Combine(work,"result.txt"),"Mini C++ 更新",MessageBoxButtons.OK,MessageBoxIcon.Warning);
                if(target!=null&&!File.Exists(Path.Combine(target,UpdateTransaction.JournalName)))try{Process.Start(new ProcessStartInfo(Path.Combine(target,"MiniCpp.exe")){UseShellExecute=true,WorkingDirectory=target});}catch(Exception){}
                return 1;
            }
        }
        static void ClearDownload(string work){
            try{
                foreach(string name in new[]{"verified","apply"}){
                    string stage=Path.Combine(work,name),manifest=Path.Combine(stage,UpdatePackage.ManifestName);
                    if(!File.Exists(manifest))continue;
                    foreach(var item in UpdatePackage.ReadXml(manifest).Elements("File")){
                        string relative=(string)item.Attribute("path");
                        if(UpdatePackage.OwnedPath(relative)){string file=UpdateTransaction.TargetPath(stage,relative);if(File.Exists(file))File.Delete(file);}
                    }
                    File.Delete(manifest);
                    foreach(string child in new[]{"assets","LICENSES"}){string directory=Path.Combine(stage,child);if(Directory.Exists(directory)&&!Directory.EnumerateFileSystemEntries(directory).Any())Directory.Delete(directory);}
                    if(!Directory.EnumerateFileSystemEntries(stage).Any())Directory.Delete(stage);
                }
                string zip=Path.Combine(work,"update.zip");if(File.Exists(zip))File.Delete(zip);
            }catch(IOException){}catch(UnauthorizedAccessException){}
        }
        static void UpdateInstallRecord(string target,string version){
            if(File.Exists(Path.Combine(target,"portable.mode")))return;
            try{
                using(var user=RegistryKey.OpenBaseKey(RegistryHive.CurrentUser,RegistryView.Registry64))
                using(var key=user.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\MiniCpp.Dev",true)){
                    if(key!=null&&string.Equals((string)key.GetValue("InstallLocation"),target,StringComparison.OrdinalIgnoreCase))key.SetValue("DisplayVersion",version);
                }
            }catch(UnauthorizedAccessException){}catch(System.Security.SecurityException){}
        }
    }
}
