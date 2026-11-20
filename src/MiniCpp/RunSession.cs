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
        public static async Task<string> LaunchAsync(string exe,string source,string bin,bool file,CancellationToken token){
            string host=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"MiniCpp.Console.exe"),cwd=Path.GetDirectoryName(source);
            string dest=Path.Combine(Native.DataDir,"sessions",Guid.NewGuid().ToString("N")+".xml");Directory.CreateDirectory(Path.GetDirectoryName(dest));
            var i=Native.StartInfo(host,Native.Args(exe,cwd,dest,bin,file?"1":"0"),cwd,bin);i.CreateNoWindow=false;
            using(var p=Process.Start(i)){
                try{while(!File.Exists(dest)){token.ThrowIfCancellationRequested();if(p.HasExited&&!File.Exists(dest))return "命令行已关闭，运行中断。";await Task.Delay(75,token).ConfigureAwait(false);}
                    var settings=new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=64000};XElement record;using(var r=XmlReader.Create(dest,settings))record=XElement.Load(r);
                    if((bool?)record.Attribute("aborted")==true)return "运行已中止。";
                    int rc=(int?)record.Attribute("exit")??-1;
                    if(file&&rc==0)return "已完成文件输入输出";
                    var sb=new StringBuilder();foreach(var row in record.Elements("row")){string k=(string)row.Attribute("kind");sb.Append(k=="in"?"输入  ":k=="out"?"输出  ":k=="err"?"错误  ":"").AppendLine(row.Value);}if(sb.Length==0)sb.AppendLine("无输入输出");sb.Append("退出码 ").Append(rc);return sb.ToString();
                }catch(OperationCanceledException){Native.Kill(p);throw;}finally{try{File.Delete(dest);}catch(IOException){}}
            }
        }
    }
}
