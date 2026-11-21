using System;
using System.IO;
using System.Xml.Linq;
using MiniCpp;

namespace MiniCppConsole {
    // The console may stay open for Enter after exit. Close every spool before publishing the manifest.
    internal sealed class Transcript {
        sealed class Channel {
            readonly string path, name;
            StreamWriter writer;
            long chars, lines, lastStart;
            bool atStart=true, afterCR;
            public Channel(string report,string name){this.name=name;path=RunFiles.StreamPath(report,name);}
            public void Append(string text){
                if(writer==null)writer=new StreamWriter(new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.Read),RunFiles.TextEncoding,8192);
                writer.Write(text);
                foreach(char ch in text){
                    if(afterCR&&ch=='\n'){chars++;afterCR=false;continue;}
                    if(atStart){lastStart=chars;lines++;}
                    afterCR=ch=='\r';atStart=afterCR||ch=='\n';chars++;
                }
            }
            public XElement Finish(){if(writer!=null)writer.Dispose();return new XElement(name,new XAttribute("chars",chars),new XAttribute("lines",lines),new XAttribute("last",lastStart));}
        }
        readonly string manifest;
        readonly Channel input,output,error;
        readonly object inputGate=new object();
        volatile string failure;
        bool finished;
        public Transcript(string manifest){this.manifest=manifest;input=new Channel(manifest,RunFiles.Input);output=new Channel(manifest,RunFiles.Output);error=new Channel(manifest,RunFiles.Error);}
        void Record(Channel channel,string text){
            if(failure!=null)return;
            try{channel.Append(text);}catch(IOException e){failure=e.Message;}catch(UnauthorizedAccessException e){failure=e.Message;}
        }
        public void SendLine(StreamWriter stdin,string text){
            lock(inputGate){if(finished)return;stdin.WriteLine(text);Record(input,text+Environment.NewLine);}
        }
        public void Print(bool isError,string text){
            (isError?Console.Error:Console.Out).Write(text);
            Record(isError?error:output,text);
        }
        public void Finish(int code,bool aborted,bool file){
            lock(inputGate){finished=true;}
            var run=new XElement("run",new XAttribute("format",2),new XAttribute("exit",code),new XAttribute("aborted",aborted),new XAttribute("file",file));
            foreach(var c in new[]{input,output,error})try{run.Add(c.Finish());}catch(IOException e){failure=e.Message;}
            if(failure!=null)run.SetAttributeValue("failure",failure);
            string part=manifest+".part";
            using(var f=new FileStream(part,FileMode.CreateNew,FileAccess.Write,FileShare.None))new XDocument(run).Save(f);
            File.Move(part,manifest);
        }
    }
}
