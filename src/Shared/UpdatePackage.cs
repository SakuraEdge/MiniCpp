using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace MiniCpp {
    internal sealed class UpdateFile {
        internal string Path,Hash;
    }

    internal sealed class UpdatePackage {
        internal const string ManifestName="update-manifest.xml";
        internal string Version;
        internal readonly List<UpdateFile> Files=new List<UpdateFile>();
        internal static string HashFile(string path){
            using(var hash=SHA256.Create())using(var input=File.OpenRead(path))return BitConverter.ToString(hash.ComputeHash(input)).Replace("-","").ToLowerInvariant();
        }
        internal static bool OwnedPath(string path){
            if(string.IsNullOrEmpty(path)||path.Contains("..")||path.Contains(":")||path.Contains("\\")||path.StartsWith("/",StringComparison.Ordinal))return false;
            if(new[]{"MiniCpp.exe","MiniCpp.exe.config","MiniCpp.Console.exe","MiniCpp.Updater.exe","ScintillaNET.dll","LICENSE","README.md",ManifestName}.Contains(path,StringComparer.OrdinalIgnoreCase))return true;
            return Regex.IsMatch(path,@"^(assets/[a-zA-Z0-9_-]+\.(png|ico)|LICENSES/[a-zA-Z0-9_.-]+\.txt)$",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant);
        }
        internal static XElement ReadXml(string path){
            using(var reader=XmlReader.Create(path,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=2*1024*1024}))return XElement.Load(reader);
        }
        internal static UpdatePackage Read(string directory,string expectedVersion){
            var root=ReadXml(Path.Combine(directory,ManifestName));
            if(root.Name!="MiniCppUpdate"||(string)root.Attribute("version")!=expectedVersion)throw new InvalidDataException("更新包版本不匹配。");
            var package=new UpdatePackage{Version=expectedVersion};var names=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach(var element in root.Elements("File")){
                var file=new UpdateFile{Path=(string)element.Attribute("path"),Hash=(string)element.Attribute("sha256")};
                if(!OwnedPath(file.Path)||string.Equals(file.Path,ManifestName,StringComparison.OrdinalIgnoreCase)||!Regex.IsMatch(file.Hash??"",@"^[0-9a-f]{64}$")||!names.Add(file.Path))throw new InvalidDataException("更新包文件清单无效。");
                package.Files.Add(file);
            }
            if(package.Files.Count>512||!new[]{"MiniCpp.exe","MiniCpp.exe.config","MiniCpp.Console.exe","MiniCpp.Updater.exe","ScintillaNET.dll","assets/mini-cpp.ico","assets/cpp-file.ico","LICENSE"}.All(names.Contains))throw new InvalidDataException("更新包缺少必需组件。");
            foreach(var file in package.Files){
                string path=System.IO.Path.Combine(directory,file.Path);
                if(!File.Exists(path)||HashFile(path)!=file.Hash)throw new InvalidDataException("更新文件校验失败："+file.Path);
            }
            return package;
        }
        internal static void Extract(string zip,string destination,string expectedVersion){
            Directory.CreateDirectory(destination);long total=0;var names=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using(var archive=ZipFile.OpenRead(zip)){
                if(archive.Entries.Count>513)throw new InvalidDataException("更新包文件过多。");
                foreach(var entry in archive.Entries){
                    if(!OwnedPath(entry.FullName)||!names.Add(entry.FullName)||(entry.ExternalAttributes&0x400)!=0||((entry.ExternalAttributes>>16)&0xf000)==0xa000)throw new InvalidDataException("更新包包含不支持的路径。");
                    if(entry.Length<0||entry.Length>256L*1024*1024-total)throw new InvalidDataException("更新包展开后过大。");total+=entry.Length;
                    string path=Path.Combine(destination,entry.FullName);Directory.CreateDirectory(Path.GetDirectoryName(path));
                    using(var input=entry.Open())using(var output=new FileStream(path,FileMode.CreateNew,FileAccess.Write)){
                        var buffer=new byte[65536];long written=0;int count;
                        while((count=input.Read(buffer,0,buffer.Length))>0){written+=count;if(written>entry.Length)throw new InvalidDataException("更新文件大小异常。");output.Write(buffer,0,count);}
                        if(written!=entry.Length)throw new InvalidDataException("更新文件不完整。");
                    }
                }
            }
            var package=Read(destination,expectedVersion);
            if(names.Count!=package.Files.Count+1)throw new InvalidDataException("更新包包含清单外文件。");
            if(System.Diagnostics.FileVersionInfo.GetVersionInfo(Path.Combine(destination,"MiniCpp.exe")).ProductVersion!=expectedVersion)throw new InvalidDataException("程序版本与更新清单不匹配。");
        }
    }
}
