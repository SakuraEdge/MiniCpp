using System;
using System.IO;
using System.Threading;
using System.Xml;
using System.Xml.Linq;

namespace MiniCpp {
    // Text stays on disk. A UI request reads at most one page, including for a single huge line.
    internal sealed class RecordedStream {
        public const int PageSize=32768;
        readonly string path;
        readonly long lastStart;
        public readonly long Chars, Lines;
        public long Pages { get { return Chars==0?1:(Chars-1)/PageSize+1; } }
        public bool CanFold { get { return Lines>10||Chars>PageSize; } }

        public RecordedStream(string manifest,string name,XElement metadata){
            if(metadata==null)throw new InvalidDataException("运行记录缺少数据流。");
            path=RunFiles.StreamPath(manifest,name);
            Chars=(long)metadata.Attribute("chars");Lines=(long)metadata.Attribute("lines");lastStart=(long)metadata.Attribute("last");
            if(Chars<0||Lines<0||Lines>Chars||lastStart<0||lastStart>Chars||
               (Chars>0&&(Lines==0||lastStart==Chars||Chars>long.MaxValue/2||new FileInfo(path).Length!=Chars*2)))
                throw new InvalidDataException("运行记录不完整。");
        }
        string Read(long offset,int count){
            if(count==0)return "";
            var bytes=new byte[count*2];
            using(var f=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read|FileShare.Delete)){
                f.Position=checked(offset*2);int pos=0;
                while(pos<bytes.Length){int n=f.Read(bytes,pos,bytes.Length-pos);if(n==0)throw new EndOfStreamException();pos+=n;}
            }
            // Read UTF-16 code units, including an unpaired unit at a provisional page boundary.
            var chars=new char[count];Buffer.BlockCopy(bytes,0,chars,0,bytes.Length);return new string(chars);
        }
        long Boundary(long page){
            if(page<=0)return 0;if(page>=Pages)return Chars;
            long offset=page*PageSize;string pair=Read(offset-1,2);
            return char.IsSurrogatePair(pair,0)||pair=="\r\n"?offset-1:offset;
        }
        public string Page(long page){long a=Boundary(page),b=Boundary(page+1);return Display(Read(a,(int)(b-a)));}
        static string Display(string text){return text.Replace("\r\n","\n").Replace('\r','\n').Replace("\n","\r\n").Replace('\0','␀');}
        public string Summary(){
            if(!CanFold)return Page(0);
            string first=Page(0);int end=0;
            if(Lines>10){for(int i=0;i<3;i++){int n=first.IndexOf("\r\n",end,StringComparison.Ordinal);if(n<0){end=first.Length;break;}end=n+2;}}
            else end=Math.Min(2048,first.Length);
            if(end>0&&char.IsHighSurrogate(first[end-1]))end--;
            long start=Math.Max(lastStart,Chars-2048);
            if(start>0&&start<Chars&&char.IsSurrogatePair(Read(start-1,2),0))start--;
            string tail=Display(Read(start,(int)(Chars-start)));
            string omitted=Lines>10?"… 省略 "+(Lines-4)+" 行 …":"…";
            return first.Substring(0,end).TrimEnd('\r','\n')+"\r\n"+omitted+"\r\n"+(start>lastStart?"…":"")+tail;
        }
    }

    internal sealed class RunRecord : IDisposable {
        public readonly string Message;
        public readonly int? ExitCode;
        public readonly bool Aborted;
        public readonly RecordedStream Input,Output,Error;
        string manifest;
        public RunRecord(string message,bool aborted=false){Message=message;Aborted=aborted;}
        RunRecord(string path,XElement root){
            if(root==null||root.Name!="run"||(int?)root.Attribute("format")!=2)throw new InvalidDataException("不能识别运行记录，请确认编辑器和命令行宿主来自同一次构建。");
            ExitCode=(int)root.Attribute("exit");Aborted=(bool)root.Attribute("aborted");
            string failure=(string)root.Attribute("failure");
            if(failure!=null)throw new IOException("保存运行记录失败："+failure);
            if((bool)root.Attribute("file")&&ExitCode==0&&!Aborted)Message="已完成文件输入输出";
            else{
                Input=new RecordedStream(path,RunFiles.Input,root.Element(RunFiles.Input));
                Output=new RecordedStream(path,RunFiles.Output,root.Element(RunFiles.Output));
                Error=new RecordedStream(path,RunFiles.Error,root.Element(RunFiles.Error));
            }
            manifest=path;
        }
        public static RunRecord Load(string path){
            var settings=new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=16384};
            using(var r=XmlReader.Create(path,settings))return new RunRecord(path,XElement.Load(r));
        }
        public void Dispose(){string path=Interlocked.Exchange(ref manifest,null);if(path!=null)RunFiles.Remove(path);}
    }
}
