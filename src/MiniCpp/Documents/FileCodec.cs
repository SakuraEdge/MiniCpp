using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using ScintillaNET;

namespace MiniCpp {
    internal static class FileCodec {
        public static string Read(string path,out Encoding enc){byte[] b=File.ReadAllBytes(path);if(b.Length>=2&&((b[0]==255&&b[1]==254)||(b[0]==254&&b[1]==255)))throw new InvalidDataException("请将 UTF-16 源文件另存为 UTF-8 后打开。");
            bool bom=b.Length>=3&&b[0]==239&&b[1]==187&&b[2]==191;try{enc=new UTF8Encoding(bom,true);return enc.GetString(b,bom?3:0,b.Length-(bom?3:0));}catch(DecoderFallbackException){enc=Encoding.GetEncoding(54936,EncoderFallback.ExceptionFallback,DecoderFallback.ExceptionFallback);return enc.GetString(b);}}
        public static void Write(string path,string content,Encoding enc){string tmp=Path.Combine(Path.GetDirectoryName(path),".minicpp-"+Guid.NewGuid().ToString("N")+".tmp");try{using(var f=new FileStream(tmp,FileMode.CreateNew,FileAccess.Write,FileShare.None))using(var w=new StreamWriter(f,enc))w.Write(content);if(File.Exists(path))File.Replace(tmp,path,null);else File.Move(tmp,path);}finally{if(File.Exists(tmp))File.Delete(tmp);}}
    }
}
