using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MiniCpp {
    internal sealed class DebugVariable { public string Name,Type,Value; }
    internal sealed class DebugSnapshot { public string File,Function; public int Line; public bool Sample; public List<DebugVariable> Variables=new List<DebugVariable>(); }

    // MI uses recursive lists of values or results and permits repeated keys (e.g. frame=...).
    internal sealed class MiNode {
        public string Key="",Text=""; public List<MiNode> Children=new List<MiNode>();
        public MiNode Get(string key){return Children.FirstOrDefault(n=>n.Key==key)??new MiNode();}
        public string Str(string key){return Get(key).Text;}
        public static MiNode Parse(string s){int i=0;var root=new MiNode();while(i<s.Length){if(s[i]==','){i++;continue;}int old=i;root.Children.Add(Read(s,ref i,0));if(i==old)break;}return root;}
        static MiNode Read(string s,ref int i,int depth){
            if(depth>48)throw new FormatException("MI 嵌套过深");var n=new MiNode();int a=i;
            while(i<s.Length&&(char.IsLetterOrDigit(s[i])||s[i]=='-'||s[i]=='_'))i++;
            if(i<s.Length&&s[i]=='='){n.Key=s.Substring(a,i-a);i++;}else i=a;
            if(i>=s.Length)return n;char c=s[i++];
            if(c=='{'||c=='['){char end=c=='{'?'}':']';while(i<s.Length&&s[i]!=end){if(s[i]==','){i++;continue;}int old=i;n.Children.Add(Read(s,ref i,depth+1));if(i==old)throw new FormatException();}if(i<s.Length)i++;}
            else if(c=='"'){var b=new StringBuilder();var oct=new List<byte>();Action flush=delegate{if(oct.Count!=0){b.Append(Encoding.UTF8.GetString(oct.ToArray()));oct.Clear();}};
                while(i<s.Length){char v=s[i++];if(v=='"')break;if(v!='\\'){flush();b.Append(v);continue;}if(i==s.Length)break;v=s[i++];if(v>='0'&&v<='7'){int k=v-'0',j=1;while(j<3&&i<s.Length&&s[i]>='0'&&s[i]<='7'){k=k*8+s[i++]-'0';j++;}oct.Add((byte)k);continue;}flush();b.Append(v=='n'?'\n':v=='r'?'\r':v=='t'?'\t':v);}
                flush();n.Text=b.ToString();}
            else{a=i-1;while(i<s.Length&&s[i]!=','&&s[i]!=']'&&s[i]!='}')i++;n.Text=s.Substring(a,i-a);}return n;
        }
    }

    internal sealed class DebugSession:IDisposable {
        readonly string bin,exe,source; readonly object gate=new object();readonly Dictionary<int,TaskCompletionSource<MiNode>> pending=new Dictionary<int,TaskCompletionSource<MiNode>>();readonly Dictionary<int,string> points=new Dictionary<int,string>();
        Process proc;StreamWriter input;ChildJob job;int seq,ended;volatile bool live,interruptForSample,interrupted;volatile bool disposed;int stopVersion;
        readonly SemaphoreSlim operations=new SemaphoreSlim(1,1);
        public bool IsRunning{get{return live;}} public bool LiveSampling {get;set;}
        public event Action<DebugSnapshot> Stopped; public event Action Running;public event Action<string> Log,Ended;
        public DebugSession(string bin,string exe,string source){this.bin=bin;this.exe=exe;this.source=source;LiveSampling=true;}
        static string Q(string s){return "\""+s.Replace("\\","\\\\").Replace("\"","\\\"").Replace("\r","\\r").Replace("\n","\\n")+"\"";}
        void Note(string s){var cb=Log;if(cb!=null&&!disposed)cb(s);}
        public async Task StartAsync(IEnumerable<int> breaks){
            if(!File.Exists(Path.Combine(bin,"gdb.exe")))throw new FileNotFoundException("工具链中缺少 gdb.exe。");
            var si=Native.StartInfo(Path.Combine(bin,"gdb.exe"),"--quiet --nx --interpreter=mi2 -iex "+Native.Quote("set auto-load off"),Path.GetDirectoryName(source),bin);
            si.RedirectStandardInput=si.RedirectStandardOutput=si.RedirectStandardError=true;si.StandardOutputEncoding=si.StandardErrorEncoding=Native.Utf8;si.EnvironmentVariables["LC_ALL"]="C";
            proc=new Process{StartInfo=si,EnableRaisingEvents=true};job=new ChildJob();proc.OutputDataReceived+=(s,e)=>{if(e.Data!=null)Handle(e.Data);};proc.ErrorDataReceived+=(s,e)=>{if(e.Data!=null)Note(e.Data);};proc.Exited+=(s,e)=>End("调试会话已结束");
            proc.Start();job.Add(proc);input=new StreamWriter(proc.StandardInput.BaseStream,Native.Utf8){AutoFlush=true};proc.BeginOutputReadLine();proc.BeginErrorReadLine();
            await Cmd("-gdb-set pagination off");await Cmd("-gdb-set confirm off");await Cmd("-gdb-set mi-async on");await Cmd("-gdb-set new-console on");await Cmd("-gdb-set print elements 24");await Cmd("-gdb-set host-charset UTF-8");
            await Cmd("-file-exec-and-symbols "+Q(exe.Replace('\\','/')));
            var lines=breaks.Distinct().ToList();foreach(int l in lines)await SetBreakpointAsync(l,true);if(lines.Count==0)await Cmd("-break-insert -t main");
            await Cmd("-exec-run");
        }
        async Task<MiNode> Cmd(string command){
            int id;var t=new TaskCompletionSource<MiNode>(TaskCreationOptions.RunContinuationsAsynchronously);
            lock(gate){if(disposed||ended!=0)throw new InvalidOperationException("调试会话已经结束");id=++seq;pending.Add(id,t);try{input.WriteLine(id.ToString()+command);}catch{pending.Remove(id);throw;}}
            try{if(await Task.WhenAny(t.Task,Task.Delay(12000)).ConfigureAwait(false)!=t.Task)throw new TimeoutException("GDB 未响应："+command.Split(' ')[0]);return await t.Task.ConfigureAwait(false);}finally{lock(gate)pending.Remove(id);}
        }
        public async Task SetBreakpointAsync(int line,bool enabled){
            if(live)throw new InvalidOperationException("请暂停后修改断点。");if(line<1)return;
            if(enabled){if(points.ContainsKey(line))return;var n=await Cmd("-break-insert "+Q(source.Replace('\\','/')+":"+line));string num=n.Get("bkpt").Str("number");if(num!="")points[line]=num;}
            else{string num;if(points.TryGetValue(line,out num)){await Cmd("-break-delete "+num);points.Remove(line);}}
        }
        public async Task ContinueAsync(){await Cmd("-exec-continue");}
        public async Task StepAsync(){await Cmd("-exec-next");}
        public async Task PauseAsync(){interrupted=true;interruptForSample=false;if(live)await Cmd("-exec-interrupt --all");}
        public async Task SampleAsync(){if(!live||disposed||!LiveSampling||interruptForSample||interrupted)return;interruptForSample=true;try{await Cmd("-exec-interrupt --all");}catch{interruptForSample=false;LiveSampling=false;}}
        void Handle(string line){try{
            if(line.Length==0||disposed)return;int pos=0;while(pos<line.Length&&char.IsDigit(line[pos]))pos++;if(pos>=line.Length)return;char type=line[pos];string rest=line.Substring(pos+1);int comma=rest.IndexOf(',');string cls=comma<0?rest:rest.Substring(0,comma);var node=comma<0?new MiNode():((type=='^'||type=='*'||type=='=')?MiNode.Parse(rest.Substring(comma+1)):new MiNode());
            if(type=='^'){int id;TaskCompletionSource<MiNode> t=null;if(int.TryParse(line.Substring(0,pos),out id)){lock(gate)pending.TryGetValue(id,out t);}if(t!=null){if(cls=="error")t.TrySetException(new InvalidOperationException(node.Str("msg")));else t.TrySetResult(node);}return;}
            if(type=='*'&&cls=="running"){live=true;interrupted=false;var cb=Running;if(cb!=null)cb();return;}
            if(type=='*'&&cls=="stopped"){live=false;string reason=node.Str("reason");if(reason.StartsWith("exited")){End("调试结束"+(node.Str("exit-code")==""?"":" · 退出码 "+node.Str("exit-code")));return;}
                string signal=node.Str("signal-name");bool sample=interruptForSample&&!interrupted&&(reason==""||reason=="signal-received"&&(signal=="SIGINT"||signal=="SIGTRAP"));interruptForSample=false;
                Observe(node,sample,Interlocked.Increment(ref stopVersion));return;}
            if(type=='&'||type=='~'){var n=MiNode.Parse(rest).Children.FirstOrDefault();if(n!=null&&n.Text.Trim()!="")Note(n.Text.TrimEnd());}
        }catch(Exception ex){Note(ex.Message);}}
        async void Observe(MiNode eventNode,bool sample,int version){await operations.WaitAsync().ConfigureAwait(false);try{
            if(disposed||ended!=0||version!=stopVersion)return;MiNode f=eventNode.Get("frame");string file=f.Str("fullname");
            if(!Match(file)){var stack=await Cmd("-stack-list-frames 0 40").ConfigureAwait(false);var better=stack.Get("stack").Children.FirstOrDefault(n=>Match(n.Str("fullname")));if(better!=null){await Cmd("-stack-select-frame "+better.Str("level")).ConfigureAwait(false);f=better;file=f.Str("fullname");}}
            int line;int.TryParse(f.Str("line"),out line);var snap=new DebugSnapshot{File=file,Line=line,Function=f.Str("func"),Sample=sample&&!interrupted};
            if(line>0){var vars=(await Cmd("-stack-list-variables --simple-values").ConfigureAwait(false)).Get("variables").Children;
                var expanded=new Dictionary<string,string>();if(vars.Any(v=>v.Str("value")=="")){var all=(await Cmd("-stack-list-variables --all-values").ConfigureAwait(false)).Get("variables").Children;foreach(var v in all)expanded[v.Str("name")]=v.Str("value");}
                foreach(var v in vars){string val=v.Str("value"),name=v.Str("name");if(val==""&&!expanded.TryGetValue(name,out val))val="（不可用）";snap.Variables.Add(new DebugVariable{Name=name,Type=v.Str("type"),Value=val});}}
            if(version==stopVersion&&!disposed&&!live){var cb=Stopped;if(cb!=null)cb(snap);}
        }catch(Exception ex){if(!disposed)Note(ex.Message);}finally{operations.Release();}
            if(sample&&!interrupted&&!disposed&&ended==0&&!live&&version==stopVersion)try{await Cmd("-exec-continue").ConfigureAwait(false);}catch(Exception ex){Note(ex.Message);}}
        bool Match(string p){try{return !string.IsNullOrEmpty(p)&&string.Equals(Path.GetFullPath(p),source,StringComparison.OrdinalIgnoreCase);}catch{return false;}}
        void End(string message){if(Interlocked.Exchange(ref ended,1)!=0)return;live=false;lock(gate){foreach(var t in pending.Values)t.TrySetException(new InvalidOperationException(message));}var cb=Ended;if(!disposed&&cb!=null)cb(message);}
        public void Dispose(){if(disposed)return;disposed=true;End("调试已停止");if(job!=null)job.Dispose();Native.Kill(proc);if(proc!=null)proc.Dispose();}
    }
}
