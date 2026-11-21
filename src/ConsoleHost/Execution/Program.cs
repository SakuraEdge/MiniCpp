using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using MiniCpp;

namespace MiniCppConsole {
    internal static class Program {
        static void Pump(StreamReader reader,bool isError,Transcript log){char[] buf=new char[4096];int n;while((n=reader.Read(buf,0,buf.Length))>0)log.Print(isError,new string(buf,0,n));}
        public static int Main(string[] args){
            if(args.Length!=5)return 2;
            string exe=args[0],cwd=args[1],report=args[2],bin=args[3];bool file=args[4]=="1",abort=false;int code=-1;
            var log=new Transcript(report);var dismiss=new ManualResetEventSlim(false);
            try{
                if(!Console.IsInputRedirected)Console.InputEncoding=Native.Utf8;else Console.SetIn(new StreamReader(Console.OpenStandardInput(),Native.Utf8));
                if(!Console.IsOutputRedirected){Console.OutputEncoding=Native.Utf8;Console.Title="Mini C++ · 命令行 · "+Path.GetFileName(cwd);}else Console.SetOut(new StreamWriter(Console.OpenStandardOutput(),Native.Utf8){AutoFlush=true});
                var info=Native.StartInfo(exe,"",cwd,bin);info.RedirectStandardInput=info.RedirectStandardOutput=info.RedirectStandardError=true;info.StandardOutputEncoding=info.StandardErrorEncoding=Native.Utf8;
                using(var p=new Process{StartInfo=info})using(var job=new ChildJob()){
                    p.Start();job.Add(p);
                    ConsoleCancelEventHandler cancel=delegate(object sender,ConsoleCancelEventArgs e){e.Cancel=true;abort=true;job.Dispose();Native.Kill(p);};Console.CancelKeyPress+=cancel;
                    try{
                        var o=Task.Run(()=>Pump(p.StandardOutput,false,log));var e=Task.Run(()=>Pump(p.StandardError,true,log));
                        var input=new Thread(delegate(){try{using(var w=new StreamWriter(p.StandardInput.BaseStream,Native.Utf8,1024,true){AutoFlush=true}){string line;while((line=Console.ReadLine())!=null){if(p.HasExited)break;log.SendLine(w,line);}}p.StandardInput.Close();}catch(IOException){}catch(InvalidOperationException){}finally{dismiss.Set();} });input.IsBackground=true;input.Start();
                        p.WaitForExit();code=p.ExitCode;job.Dispose();Task.WaitAll(o,e);
                    }finally{Console.CancelKeyPress-=cancel;}
                }
            }catch(Exception ex){abort=true;dismiss.Set();log.Print(true,ex.Message+Environment.NewLine);}
            try{log.Finish(code,abort,file);}catch(Exception ex){Console.Error.WriteLine(ex.Message);return 3;}
            if(!Console.IsInputRedirected&&!Console.IsOutputRedirected){Console.WriteLine("\n["+(abort?"已中止":"程序已结束，退出码 "+code)+"]");if(!dismiss.IsSet){Console.WriteLine("按 Enter 关闭窗口。");dismiss.Wait();}}
            return code;
        }
    }
}
