using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Xml;
using System.Xml.Linq;

namespace MiniCpp {
    internal sealed class CodeTemplate {
        public string Command,Code;
        public override string ToString(){return Command;}
    }

    internal sealed class TemplateSettings {
        internal const string Cursor="${cursor}";
        internal string Initial;
        internal readonly List<CodeTemplate> Commands=new List<CodeTemplate>();
        internal string ReadError;
        string baseline;
        static string FileName { get { return Path.Combine(Native.DataDir,"templates.xml"); } }

        internal static TemplateSettings Defaults(){
            const string header="#include <bits/stdc++.h>\nusing namespace std;\n";
            var settings=new TemplateSettings{Initial=SourceDocument.Template.Replace("    \r\n","    ${cursor}\r\n")};
            settings.Commands.Add(new CodeTemplate{Command="/main",Code=header+"int main() {\n    ${cursor}return 0;\n}"});
            settings.Commands.Add(new CodeTemplate{Command="/fremain",Code=header+"int main() {\n    freopen(\".in\",\"r\",stdin);\n    freopen(\".out\",\"w\",stdout);\n    ${cursor}return 0;\n}"});
            settings.Commands.Add(new CodeTemplate{Command="/solve",Code=header+"\nvoid solve() {\n    ${cursor}\n}\n\nint main() {\n    solve();\n    return 0;\n}"});
            return settings;
        }

        internal static TemplateSettings Load(){
            var settings=Defaults();
            try {
                if(!File.Exists(FileName))return settings;
                settings.baseline=File.ReadAllText(FileName,Native.Utf8);
                using(var input=new StringReader(settings.baseline))
                using(var reader=XmlReader.Create(input,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=2*1024*1024})){
                    var root=XElement.Load(reader);
                    if(root.Name!="Templates"||root.Element("Initial")==null)throw new InvalidDataException("框架配置格式不正确。");
                    var loaded=new TemplateSettings{Initial=root.Element("Initial").Value,baseline=settings.baseline};
                    foreach(var item in root.Elements("Command"))loaded.Commands.Add(new CodeTemplate{Command=(string)item.Attribute("name"),Code=item.Value});
                    loaded.Validate();return loaded;
                }
            } catch(Exception ex){
                if(!(ex is IOException||ex is UnauthorizedAccessException||ex is XmlException||ex is ArgumentException))throw;
                settings.ReadError="未能读取已保存框架，当前显示默认内容。"+ex.Message;return settings;
            }
        }

        internal void Validate(){
            if(Initial==null||Initial.Length>200000||Commands.Count>100)throw new ArgumentException("框架内容过长或快捷指令过多。");
            var names=new HashSet<string>(StringComparer.Ordinal);
            foreach(var item in Commands){
                if(!Regex.IsMatch(item.Command??"",@"^/[\p{L}_][\p{L}\p{N}_]{0,63}$"))throw new ArgumentException("指令须以 / 开头，后接字母、数字或下划线，例如 /main。");
                if(!names.Add(item.Command))throw new ArgumentException("快捷指令重复："+item.Command);
                if(string.IsNullOrWhiteSpace(item.Code)||item.Code.Length>200000)throw new ArgumentException(item.Command+" 的框架不能为空或超过 200000 字符。");
            }
        }

        internal void Save(){
            Validate();Directory.CreateDirectory(Native.DataDir);
            using(var mutex=new Mutex(false,@"Local\MiniCpp.Templates.Writer")){
                try{if(!mutex.WaitOne(2000))throw new IOException("其他窗口正在保存框架，请稍后重试。");}catch(AbandonedMutexException){}
                try{
                    string latest=File.Exists(FileName)?File.ReadAllText(FileName,Native.Utf8):null;
                    if(latest!=baseline)throw new IOException("框架已在其他窗口中修改，请重新打开设置后编辑。");
                    var root=new XElement("Templates",new XElement("Initial",Initial));
                    foreach(var item in Commands)root.Add(new XElement("Command",new XAttribute("name",item.Command),item.Code));
                    string text=root.ToString(),temp=FileName+"."+Guid.NewGuid().ToString("N")+".tmp";
                    if(text.Length>2*1024*1024)throw new ArgumentException("框架配置总大小超过 2 MB，请缩减内容。");
                    try{
                        File.WriteAllText(temp,text,Native.Utf8);
                        if(File.Exists(FileName))File.Replace(temp,FileName,FileName+".bak");else File.Move(temp,FileName);
                        baseline=text;
                    }finally{if(File.Exists(temp))File.Delete(temp);}
                }finally{mutex.ReleaseMutex();}
            }
        }

        internal static string Expand(string template,string newline,out int caret){
            string text=template.Replace("\r\n","\n").Replace("\r","\n").Replace("\n",newline);
            caret=text.IndexOf(Cursor,StringComparison.Ordinal);
            if(caret<0){caret=text.Length;return text;}
            return text.Replace(Cursor,"");
        }
    }
}
