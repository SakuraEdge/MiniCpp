using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace MiniCpp {
    internal sealed class Diagnostic {
        public string File, Severity, Message, Suggestion; public int Line, Column, HintLine, HintColumn;
    }
    internal sealed class BuildResult {
        public bool Success; public string Log, Executable; public List<Diagnostic> Diagnostics = new List<Diagnostic>();
    }
    internal static class Compiler {
        public static readonly string[] Standards = {"98","11","14","17"};
        public static string FindBin(string configured) {
            string root = AppDomain.CurrentDomain.BaseDirectory;
            var candidates = new List<string>{ configured, Path.Combine(root,"w64devkit","bin"), Path.Combine(root,"toolchain","bin"), Environment.GetEnvironmentVariable("MINICPP_TOOLCHAIN") };
            candidates.AddRange((Environment.GetEnvironmentVariable("PATH") ?? "").Split(';'));
            foreach (string raw in candidates) try {
                if (string.IsNullOrWhiteSpace(raw)) continue; string path = raw.Trim().Trim('"');
                if (!Path.IsPathRooted(path)) continue; path=Path.GetFullPath(path);
                if (File.Exists(path)) path=Path.GetDirectoryName(path);
                if (File.Exists(Path.Combine(path,"g++.exe"))) return path;
                if (File.Exists(Path.Combine(path,"bin","g++.exe"))) return Path.Combine(path,"bin");
            } catch(ArgumentException) {} catch(NotSupportedException) {} catch(PathTooLongException) {}
            return null;
        }
        public static string ArtifactPath(string source,bool debug) {
            using(var sha=SHA256.Create()) { string id=BitConverter.ToString(sha.ComputeHash(Native.Utf8.GetBytes(Path.GetFullPath(source).ToUpperInvariant()))).Replace("-","").Substring(0,32); return Path.Combine(Native.DataDir,"build",id,debug?"debug.exe":"program.exe"); }
        }
        static readonly Regex Position=new Regex(@"^(?<file>.+?):(?<line>\d+):(?:(?<col>\d+):)?\s*(?<kind>fatal error|error|warning|note):\s*(?<msg>.*)$",RegexOptions.Compiled);
        public static async Task<BuildResult> BuildAsync(string source,string std,bool o2,bool debug,bool rebuild,string bin,CancellationToken token) {
            if(!Standards.Contains(std)) throw new ArgumentException("不支持的语言标准");
            if(string.IsNullOrEmpty(bin))throw new InvalidOperationException("找不到 g++.exe。请使用含工具链的便携版，或在「工具 → 编译工具链」选择 GCC 的 bin 目录。");
            string output=ArtifactPath(source,debug),dir=Path.GetDirectoryName(output),cwd=Path.GetDirectoryName(source);
            Directory.CreateDirectory(dir); string temp=Path.Combine(dir,Guid.NewGuid().ToString("N")+".exe");
            var flags=new List<string>{"-std=c++"+std,debug?"-O0":o2?"-O2":"-O0","-Wall","-fdiagnostics-color=never","-fmessage-length=0"};
            Encoding sourceEncoding;string sourceText=FileCodec.Read(source,out sourceEncoding);
            if(debug)flags.AddRange(new[]{"-g3","-gdwarf-4","-fno-omit-frame-pointer"});
            string args=string.Join(" ",flags)+" "+Native.Quote(source)+" -o "+Native.Quote(temp);
            var result=new BuildResult{Executable=output};
            try {
                token.ThrowIfCancellationRequested();
                if(rebuild&&File.Exists(output))File.Delete(output);
                var info=Native.StartInfo(Path.Combine(bin,"g++.exe"),args,cwd,bin);info.RedirectStandardError=info.RedirectStandardOutput=true;info.StandardErrorEncoding=info.StandardOutputEncoding=Native.Utf8;info.EnvironmentVariables["LC_ALL"]="C";
                using(var p=new Process{StartInfo=info}) using(var job=new ChildJob()) {
                    p.Start();job.Add(p);var err=p.StandardError.ReadToEndAsync();var stdout=p.StandardOutput.ReadToEndAsync();
                    using(token.Register(delegate{job.Dispose();Native.Kill(p);})) {
                        await Task.WhenAll(err,stdout,Task.Run((Action)p.WaitForExit)).ConfigureAwait(false);token.ThrowIfCancellationRequested();
                        string raw=stdout.Result+err.Result;
                        result.Log=(rebuild?"全部重新编译（单文件）\r\n":"")+"g++ "+args+"\r\n\r\n"+raw;
                        string[] lines=Regex.Split(sourceText,"\r?\n");
                        foreach(string line in Regex.Split(raw,"\r?\n")) {var m=Position.Match(line);if(!m.Success)continue;
                            var d=new Diagnostic{File=m.Groups["file"].Value,Severity=m.Groups["kind"].Value,Message=m.Groups["msg"].Value};int.TryParse(m.Groups["line"].Value,out d.Line);int.TryParse(m.Groups["col"].Value,out d.Column);
                            try{d.File=Path.GetFullPath(Path.IsPathRooted(d.File)?d.File:Path.Combine(cwd,d.File));}catch(ArgumentException){}
                            d.HintLine=d.Line;d.HintColumn=d.Column;Suggest(d,string.Equals(source,d.File,StringComparison.OrdinalIgnoreCase)?lines:new string[0]);result.Diagnostics.Add(d);
                        }
                        result.Success=p.ExitCode==0&&File.Exists(temp);
                        if(result.Success){if(File.Exists(output))File.Replace(temp,output,null);else File.Move(temp,output);result.Log+="\r\n编译成功："+output;}
                        else{result.Log+="\r\n编译失败（退出码 "+p.ExitCode+"）";if(!result.Diagnostics.Any(x=>x.Severity.Contains("error")))result.Diagnostics.Add(new Diagnostic{File=source,Severity="error",Message=raw.Trim(),Suggestion=raw.Contains("unrecognized")?"编译器可能过旧，请使用便携版 GCC 或更换工具链。":"请查看编译日志中的第一条错误信息。"});}
                    }
                }
            } finally {try{if(File.Exists(temp))File.Delete(temp);}catch(IOException){} }
            return result;
        }
        static void Suggest(Diagnostic d,string[] lines){string s=d.Message; if(d.Severity=="note")return;
            if(s.Contains("expected ';'")||s.Contains("expected ‘;’")){
                int k=d.Line-2;while(k>=0&&k<lines.Length&&string.IsNullOrWhiteSpace(lines[k]))k--;
                if(k>=0&&k<lines.Length&&!Regex.IsMatch(lines[k].TrimEnd(),@"[;{}:]$")){d.HintLine=k+1;d.HintColumn=lines[k].TrimEnd().Length+1;d.Suggestion="可能在第 "+d.HintLine+" 行末尾漏了分号；编译器报错的位置可能在下一行。";}
                else d.Suggestion="检查该语句及上一条语句末尾是否缺少分号。";
            }else if(s.Contains("was not declared")||s.Contains("has not been declared")){var hint=Regex.Match(s,@"did you mean ['‘](.+?)['’]");d.Suggestion="检查变量名拼写、声明顺序及作用域。"+(hint.Success?"编译器建议检查 "+hint.Groups[1].Value+"。":"");}
            else if(s.Contains("expected '}'")||s.Contains("end of input"))d.Suggestion="检查是否有未配对的花括号，尤其是函数和循环结尾。";
            else if(s.Contains("expected ')'")||s.Contains("expected ']'")||s.Contains("expected primary-expression"))d.Suggestion="检查附近的括号、运算符以及表达式是否完整。";
            else if(s.Contains("No such file or directory"))d.Suggestion="检查头文件名称、相对路径以及文件是否存在。";
            else if(s.Contains("cannot convert")||s.Contains("invalid conversion")||s.Contains("no matching function"))d.Suggestion="检查调用时的参数数量、参数类型和返回值类型。";
            else if(s.Contains("missing terminating"))d.Suggestion="检查字符串或字符常量是否缺少结束引号。";
            else if(s.Contains("redefinition")||s.Contains("redeclaration"))d.Suggestion="检查是否重复定义了同名变量或函数。";
            else if(s.Contains("C++")||s.Contains("-std="))d.Suggestion="检查当前 C++ 标准是否支持这段语法。";
        }
    }
}
