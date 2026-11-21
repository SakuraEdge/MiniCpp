using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;

namespace MiniCpp {
    internal enum InputMode { Auto, Console, Files }
    internal static class RunSession {
        // Detection is deliberately conservative. Explicit file/console modes override auto-detection.
        public static bool FileMode(string code,InputMode mode){if(mode!=InputMode.Auto)return mode==InputMode.Files;string s=Regex.Replace(code,@"//[^\r\n]*|/\*[\s\S]*?\*/", "");return (Regex.IsMatch(s,@"\b(?:ifstream|fstream)\b")&&Regex.IsMatch(s,@"\b(?:ofstream|fstream)\b")&&!Regex.IsMatch(s,@"\b(?:cin|cout|printf|scanf)\b"))||(Regex.IsMatch(s,@"\bfreopen\s*\([^;]*?\bstdin\s*\)")&&Regex.IsMatch(s,@"\bfreopen\s*\([^;]*?\bstdout\s*\)"));}
        public static async Task<RunRecord> LaunchAsync(string exe,string source,string bin,bool file,CancellationToken token){
            string host=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"MiniCpp.Console.exe"),cwd=Path.GetDirectoryName(source);
            string dest=Path.Combine(Native.DataDir,"sessions",Guid.NewGuid().ToString("N")+".xml");Directory.CreateDirectory(Path.GetDirectoryName(dest));
            var i=Native.StartInfo(host,Native.Args(exe,cwd,dest,bin,file?"1":"0"),cwd,bin);i.CreateNoWindow=false;
            bool retained=false;
            using(var p=Process.Start(i))using(token.Register(()=>Native.Kill(p))){
                try{while(!File.Exists(dest)){token.ThrowIfCancellationRequested();if(p.HasExited&&!File.Exists(dest))return new RunRecord("命令行已关闭，运行中断。",true);await Task.Delay(75,token).ConfigureAwait(false);}
                    token.ThrowIfCancellationRequested();var result=RunRecord.Load(dest);retained=true;return result;
                }finally{if(!retained){if(token.IsCancellationRequested){Native.Kill(p);p.WaitForExit(2000);}RunFiles.Remove(dest);}}
            }
        }
    }
}
