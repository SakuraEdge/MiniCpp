using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Microsoft.Win32;

[assembly:System.Runtime.Versioning.TargetFramework(".NETFramework,Version=v4.8")]

internal static class PortableSmoke {
    static Type setup,integration,mainForm;
    const BindingFlags InternalStatic=BindingFlags.Static|BindingFlags.NonPublic;
    static object Property(Type type,string name){return type.GetProperty(name,InternalStatic).GetValue(null,null);}
    static object Call(Type type,string name){return type.GetMethod(name,InternalStatic).Invoke(null,null);}
    static void Check(bool condition,string message){if(!condition)throw new Exception(message);}

    static void OpenEditor(bool expectPrompt){
        bool sawPrompt=false;Exception failure=null;
        using(var form=(Form)Activator.CreateInstance(mainForm,new object[]{new string[0]}))
        using(var timer=new Timer{Interval=100}){
            int ticks=0;
            timer.Tick+=(s,e)=>{
                try {
                    ticks++;
                    var prompt=Application.OpenForms.Cast<Form>().FirstOrDefault(f=>f.GetType().Name=="PortableSetupDialog");
                    if(prompt!=null){
                        sawPrompt=true;
                        Check(expectPrompt,"First-run prompt appeared again");
                        Check(prompt.Controls.Find("desktopShortcut",true).Length==1,"Desktop option missing");
                        Check(prompt.Controls.Find("defaultCpp",true).Length==1,"Default application option missing");
                        ((Button)prompt.Controls.Find("skip",true).Single()).PerformClick();
                    }
                    if(ticks>=12){timer.Stop();form.Close();}
                } catch(Exception ex){
                    failure=ex;timer.Stop();
                    foreach(var open in Application.OpenForms.Cast<Form>().ToArray())open.Close();
                }
            };
            form.Shown+=(s,e)=>timer.Start();
            Application.Run(form);
        }
        if(failure!=null)throw failure;
        Check(sawPrompt==expectPrompt,"First-run prompt was not shown");
    }

    [STAThread] public static int Main(string[] args){
        try {
            Check(args.Length==1,"Expected an isolated data directory");
            Check(Environment.GetEnvironmentVariable("GITHUB_ACTIONS")=="true","Portable integration checks run only on the GitHub runner");
            Environment.SetEnvironmentVariable("MINICPP_HOME",Path.GetFullPath(args[0]));
            Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
            var assembly=Assembly.LoadFrom(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"MiniCpp.exe"));
            setup=assembly.GetType("MiniCpp.PortableSetup",true);
            integration=assembly.GetType("MiniCpp.DesktopIntegration",true);
            mainForm=assembly.GetType("MiniCpp.MainForm",true);
            Check((bool)Property(setup,"Enabled"),"Portable marker was not included");
            string state=(string)Property(setup,"StatePath");
            Check(!File.Exists(state),"Unexpected existing first-run state");
            OpenEditor(true);
            Check(File.Exists(state),"Skipping setup was not remembered");
            OpenEditor(false);

            const string choiceKey=@"Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts\.cpp\UserChoice";
            string previous=(string)Registry.GetValue(@"HKEY_CURRENT_USER\"+choiceKey,"ProgId",null);
            Call(integration,"CreateDesktopShortcut");
            string link=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),"Mini C++ (免安装版).lnk");
            Check(File.Exists(link),"Portable desktop shortcut missing");
            Call(integration,"RegisterCpp");
            string command=(string)Registry.GetValue(@"HKEY_CURRENT_USER\Software\Classes\MiniCpp.Portable.Source.cpp\shell\open\command","",null);
            string executable=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"MiniCpp.exe");
            Check(command=="\""+executable+"\" \"%1\"","CPP association points to a different executable");
            Check(previous==(string)Registry.GetValue(@"HKEY_CURRENT_USER\"+choiceKey,"ProgId",null),"Registration changed UserChoice without Windows confirmation");

            string bin=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"w64devkit","bin");
            string source=Path.Combine(args[0],"main.cpp"),output=Path.Combine(args[0],"answer.exe");
            File.WriteAllText(source,"#include <iostream>\nint main(){std::cout << 42;}\n");
            var compile=new ProcessStartInfo(Path.Combine(bin,"g++.exe"),"-std=c++17 \""+source+"\" -o \""+output+"\""){
                UseShellExecute=false,CreateNoWindow=true,RedirectStandardError=true
            };
            compile.EnvironmentVariables["PATH"]=bin+";"+Environment.GetEnvironmentVariable("PATH");
            using(var process=Process.Start(compile)){
                string error=process.StandardError.ReadToEnd();process.WaitForExit();
                Check(process.ExitCode==0,"Portable compiler failed: "+error);
            }
            using(var process=Process.Start(new ProcessStartInfo(output){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true})){
                string answer=process.StandardOutput.ReadToEnd();process.WaitForExit();
                Check(process.ExitCode==0&&answer=="42","Portable program failed to run");
            }
            Console.WriteLine("PASS: first launch, skipped setup persistence, desktop shortcut, CPP registration, portable compile and run.");
            return 0;
        } catch(Exception ex){Console.Error.WriteLine(ex);return 1;}
    }
}
