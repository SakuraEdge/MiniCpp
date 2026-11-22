using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace MiniCpp {
    internal static class UpdateTransaction {
        internal const string JournalName=".minicpp-update.xml";
        internal static string TargetPath(string root,string relative){
            if(!UpdatePackage.OwnedPath(relative))throw new InvalidDataException("不支持的更新路径。");
            string path=Path.GetFullPath(Path.Combine(root,relative));
            if(!path.StartsWith(root.TrimEnd('\\')+"\\",StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("更新路径越界。");
            for(string check=path;check!=null;check=Path.GetDirectoryName(check)){
                if((File.Exists(check)||Directory.Exists(check))&&(File.GetAttributes(check)&FileAttributes.ReparsePoint)!=0)throw new IOException("更新目录包含链接，请使用普通文件夹。");
                if(string.Equals(check,root.TrimEnd('\\'),StringComparison.OrdinalIgnoreCase))break;
            }
            return path;
        }
        internal static void Replace(string source,string target){
            Directory.CreateDirectory(Path.GetDirectoryName(target));string temp=target+"."+Guid.NewGuid().ToString("N")+".update";
            try { File.Copy(source,temp);if(File.Exists(target))File.Replace(temp,target,null);else File.Move(temp,target); }
            finally { if(File.Exists(temp))File.Delete(temp); }
        }
        internal static void Recover(string target){
            string journal=Path.Combine(target,JournalName);if(!File.Exists(journal))return;
            var root=UpdatePackage.ReadXml(journal);Guid id;
            if(root.Name!="UpdateTransaction"||!Guid.TryParseExact((string)root.Attribute("id"),"N",out id))throw new InvalidDataException("更新恢复记录无效。");
            string backup=Path.Combine(target,".minicpp-backup-"+id.ToString("N"));
            if((File.GetAttributes(backup)&FileAttributes.ReparsePoint)!=0)throw new IOException("更新备份目录无效。");
            var files=root.Elements("File").ToArray();if(files.Length==0||files.Length>513)throw new InvalidDataException("更新恢复记录无效。");
            var names=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            // Validate every recovery record before restoring any file.
            foreach(var item in files){
                string path=(string)item.Attribute("path");TargetPath(target,path);
                if(!names.Add(path))throw new InvalidDataException("更新恢复记录包含重复文件。");
                if((string)item.Attribute("existed")=="true"){
                    string source=TargetPath(backup,path);
                    if(!File.Exists(source)||UpdatePackage.HashFile(source)!=(string)item.Attribute("sha256"))throw new IOException("更新备份损坏："+path);
                }else if((string)item.Attribute("existed")!="false")throw new InvalidDataException("更新恢复记录无效。");
            }
            foreach(var item in files){
                string path=(string)item.Attribute("path"),destination=TargetPath(target,path);
                if((string)item.Attribute("existed")=="true")Replace(Path.Combine(backup,path),destination);
                else if(File.Exists(destination))File.Delete(destination);
            }
            File.Delete(journal);RemoveBackup(backup,files.Select(x=>(string)x.Attribute("path")));
        }
        internal static void Apply(string stage,string target,string version){
            target=Path.GetFullPath(target).TrimEnd('\\');Recover(target);
            var package=UpdatePackage.Read(stage,version);
            var paths=package.Files.Select(file=>file.Path).Concat(new[]{UpdatePackage.ManifestName}).ToArray();
            string id=Guid.NewGuid().ToString("N"),backup=Path.Combine(target,".minicpp-backup-"+id),journal=Path.Combine(target,JournalName);
            Directory.CreateDirectory(backup);var record=new XElement("UpdateTransaction",new XAttribute("id",id));
            bool recorded=false;
            try {
                foreach(string path in paths){
                    string destination=TargetPath(target,path);bool exists=File.Exists(destination);
                    var item=new XElement("File",new XAttribute("path",path),new XAttribute("existed",exists));
                    if(exists){
                        using(var probe=new FileStream(destination,FileMode.Open,FileAccess.ReadWrite,FileShare.None)){}
                        string saved=Path.Combine(backup,path);Directory.CreateDirectory(Path.GetDirectoryName(saved));File.Copy(destination,saved);item.SetAttributeValue("sha256",UpdatePackage.HashFile(saved));
                    }
                    record.Add(item);
                }
                string pending=Path.Combine(backup,"journal.tmp");
                using(var stream=new FileStream(pending,FileMode.CreateNew,FileAccess.Write,FileShare.None)){record.Save(stream);stream.Flush(true);}
                File.Move(pending,journal);recorded=true;
                foreach(string path in paths)Replace(Path.Combine(stage,path),TargetPath(target,path));
                UpdatePackage.Read(target,version);File.Delete(journal);recorded=false;
            } catch {
                if(recorded)Recover(target);throw;
            } finally {
                if(!File.Exists(journal))RemoveBackup(backup,paths);
            }
        }
        static void RemoveBackup(string folder,IEnumerable<string> paths){
            // Remove only backup entries written by this transaction.
            try{
                foreach(string path in paths){string file=Path.Combine(folder,path);if(File.Exists(file))File.Delete(file);}
                string pending=Path.Combine(folder,"journal.tmp");if(File.Exists(pending))File.Delete(pending);
                foreach(string directory in new[]{"assets","LICENSES"}){string path=Path.Combine(folder,directory);if(Directory.Exists(path)&&!Directory.EnumerateFileSystemEntries(path).Any())Directory.Delete(path);}
                if(Directory.Exists(folder)&&!Directory.EnumerateFileSystemEntries(folder).Any())Directory.Delete(folder);
            }catch(IOException){}catch(UnauthorizedAccessException){}
        }
    }
}
