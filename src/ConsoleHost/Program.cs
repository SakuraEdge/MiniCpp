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
    internal sealed class Transcript {
        sealed class Row {public string Kind,Text;}
        readonly List<Row> first=new List<Row>(); readonly Queue<Row> last=new Queue<Row>();
        readonly object gate=new object(); readonly StringBuilder partial=new StringBuilder(); string stream=""; long total; bool clipped,hasPartial;
        void Commit(){var r=new Row{Kind=stream,Text=partial.ToString()+(clipped?"…":"")};if(first.Count<8)first.Add(r);else{if(last.Count==2)last.Dequeue();last.Enqueue(r);}total++;partial.Clear();hasPartial=false;clipped=false;}
        public void Append(string kind,string data,bool display){lock(gate){if(display)Console.Write(data);if(hasPartial&&kind!=stream)Commit();stream=kind;foreach(char c in data){if(c=='\r')continue;if(c=='\n'){Commit();continue;}hasPartial=true;if(partial.Length<120)partial.Append(c);else clipped=true;}}}
        static string Clean(string s){var b=new StringBuilder();for(int i=0;i<s.Length;i++){char c=s[i];if(char.IsHighSurrogate(c)&&i+1<s.Length&&char.IsLowSurrogate(s[i+1])){b.Append(c).Append(s[++i]);}else if(XmlConvert.IsXmlChar(c))b.Append(c);else b.Append('�');}return b.ToString();}
        public void Write(string dest,int code,bool abort,bool file){lock(gate){if(hasPartial)Commit();var run=new XElement("run",new XAttribute("exit",code),new XAttribute("aborted",abort),new XAttribute("file",file));if(!file||code!=0||abort){Action<Row> add=r=>run.Add(new XElement("row",new XAttribute("kind",r.Kind),Clean(r.Text)));if(total<=10){foreach(var r in first.Concat(last))add(r);}else{foreach(var r in first.Take(3))add(r);add(new Row{Kind="skip",Text="… 省略 "+(total-4)+" 行"});add(last.Last());}}string tmp=dest+".part";using(var f=new FileStream(tmp,FileMode.CreateNew,FileAccess.Write,FileShare.None))new XDocument(run).Save(f);File.Move(tmp,dest);}}
    }
    internal static class Program {
        static void Pump(StreamReader reader,string kind,Transcript log){char[] buf=new char[1024];int n;while((n=reader.Read(buf,0,buf.Length))>0)log.Append(kind,new string(buf,0,n),true);}
        public static int Main(string[] args){
            if(args.Length!=5)return 2;
            string exe=args[0],cwd=args[1],report=args[2],bin=args[3];bool file=args[4]=="1",abort=false;int code=-1;
            var log=new Transcript();var dismiss=new ManualResetEventSlim(false);
            try{
                if(!Console.IsInputRedirected)Console.InputEncoding=Native.Utf8;else Console.SetIn(new StreamReader(Console.OpenStandardInput(),Native.Utf8));
                if(!Console.IsOutputRedirected){Console.OutputEncoding=Native.Utf8;Console.Title="Mini C++ · 命令行 · "+Path.GetFileName(cwd);}else Console.SetOut(new StreamWriter(Console.OpenStandardOutput(),Native.Utf8){AutoFlush=true});
                var info=Native.StartInfo(exe,"",cwd,bin);info.RedirectStandardInput=info.RedirectStandardOutput=info.RedirectStandardError=true;info.StandardOutputEncoding=info.StandardErrorEncoding=Native.Utf8;
                using(var p=new Process{StartInfo=info})using(var job=new ChildJob()){
                    p.Start();job.Add(p);
                    ConsoleCancelEventHandler cancel=delegate(object sender,ConsoleCancelEventArgs e){e.Cancel=true;abort=true;job.Dispose();Native.Kill(p);};Console.CancelKeyPress+=cancel;
                    try{
                        var o=Task.Run(()=>Pump(p.StandardOutput,"out",log));var e=Task.Run(()=>Pump(p.StandardError,"err",log));
                        var input=new Thread(delegate(){try{using(var w=new StreamWriter(p.StandardInput.BaseStream,Native.Utf8,1024,true){AutoFlush=true}){string line;while((line=Console.ReadLine())!=null){if(p.HasExited)break;log.Append("in",line+"\n",false);w.WriteLine(line);}}p.StandardInput.Close();}catch(IOException){}catch(InvalidOperationException){}finally{dismiss.Set();} });input.IsBackground=true;input.Start();
                        p.WaitForExit();code=p.ExitCode;job.Dispose();Task.WaitAll(o,e);
                    }finally{Console.CancelKeyPress-=cancel;}
                }
            }catch(Exception ex){abort=true;dismiss.Set();log.Append("err",ex.Message+"\n",true);}
            try{log.Write(report,code,abort,file);}catch(Exception ex){Console.Error.WriteLine(ex.Message);return 3;}
            if(!Console.IsInputRedirected&&!Console.IsOutputRedirected){Console.WriteLine("\n["+(abort?"已中止":"程序已结束，退出码 "+code)+"]");if(!dismiss.IsSet){Console.WriteLine("按 Enter 关闭窗口。");dismiss.Wait();}}
            return code;
        }
    }
}
