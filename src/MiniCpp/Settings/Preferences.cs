using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Xml;
using System.Xml.Linq;

namespace MiniCpp {
    internal sealed class Preferences {
        public string Bin="",Standard="14";
        public bool O2,Sampling=true,OutputCollapsed,LargeTools,DarkTheme;
        public int OutputPercent=26;
        public readonly int[] FontSizes=(int[])FontSettings.Defaults.Clone();
        XElement baseline;
        static string FileName{get{return Path.Combine(Native.DataDir,"preferences.xml");}}

        static XElement Read(string path){
            using(var f=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete))
            using(var r=XmlReader.Create(f,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null}))return XElement.Load(r);
        }
        static bool Flag(XElement x,string key,bool fallback=false){
            string value=(string)x.Attribute(key);bool b;
            return value=="1"||value!="0"&&(bool.TryParse(value,out b)?b:fallback);
        }
        public static Preferences Load(){
            var p=new Preferences();
            try{
                var x=Read(FileName);p.DarkTheme=Flag(x,"darkTheme");p.Bin=(string)x.Attribute("bin")??"";
                string standard=(string)x.Attribute("standard");if(Compiler.Standards.Contains(standard))p.Standard=standard;
                p.O2=Flag(x,"o2");p.Sampling=Flag(x,"sampling",true);p.OutputCollapsed=Flag(x,"outputCollapsed");p.LargeTools=Flag(x,"largeTools");
                int v;if(int.TryParse((string)x.Attribute("outputPercent"),out v))p.OutputPercent=Math.Max(1,Math.Min(99,v));
                if(x.Attribute("runIoPt")==null&&int.TryParse((string)x.Attribute("outputPt"),out v)&&v!=12)p.FontSizes[4]=FontSettings.Clamp(v);
                for(int i=0;i<p.FontSizes.Length;i++)if(int.TryParse((string)x.Attribute(FontSettings.Keys[i]),out v))p.FontSizes[i]=FontSettings.Clamp(v);
            }catch(IOException){}catch(UnauthorizedAccessException){}catch(XmlException){}
            p.baseline=p.Snapshot();return p;
        }
        XElement Snapshot(){
            var x=new XElement("MiniCpp",new XAttribute("darkTheme",DarkTheme),new XAttribute("bin",Bin),new XAttribute("standard",Standard),new XAttribute("o2",O2),new XAttribute("sampling",Sampling),new XAttribute("largeTools",LargeTools),new XAttribute("outputPercent",OutputPercent),new XAttribute("outputCollapsed",OutputCollapsed));
            for(int i=0;i<FontSizes.Length;i++)x.SetAttributeValue(FontSettings.Keys[i],FontSizes[i]);
            return x;
        }
        public bool Save(){
            var current=Snapshot();
            var changes=current.Attributes().Where(a=>baseline==null||a.Value!=(string)baseline.Attribute(a.Name)).ToArray();
            if(changes.Length==0)return true;
            string path=FileName;
            try{
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                // Merge this window's edits, not its startup snapshot, with the latest on-disk settings.
                using(var mutex=new Mutex(false,@"Local\MiniCpp.Preferences.Writer")){
                    try{if(!mutex.WaitOne(1000))return false;}catch(AbandonedMutexException){}
                    try{
                        bool exists=File.Exists(path);XElement merged=current;string backup=null;
                        if(exists){try{merged=Read(path);}catch(XmlException){backup=path+".bak";}}
                        foreach(var a in changes)merged.SetAttributeValue(a.Name,a.Value);
                        string tmp=path+"."+Guid.NewGuid().ToString("N")+".tmp";
                        try{
                            using(var f=new FileStream(tmp,FileMode.CreateNew,FileAccess.Write,FileShare.None)){merged.Save(f);f.Flush(true);}
                            if(exists)File.Replace(tmp,path,backup);else File.Move(tmp,path);
                        }finally{if(File.Exists(tmp))File.Delete(tmp);}
                        baseline=current;return true;
                    }finally{mutex.ReleaseMutex();}
                }
            }catch(IOException){return false;}catch(UnauthorizedAccessException){return false;}
        }
    }
}
