using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace MiniCpp {
    [DataContract] internal sealed class UpdateDescriptor {
        [DataMember(Name="version")] public string Version;
        [DataMember(Name="file")] public string File;
        [DataMember(Name="sha256")] public string Hash;
        [DataMember(Name="size")] public long Size;
        [DataMember(Name="toolchain")] public string Toolchain;
    }
    internal sealed class DownloadProgress {
        internal long Received,Total;
    }
    internal sealed class PreparedUpdate {
        internal string Directory,Version,Hash;
    }
    internal static class UpdateDownload {
        const long Maximum=256L*1024*1024;
        static HttpClient Client(){
            var client=new HttpClient(new HttpClientHandler{AllowAutoRedirect=true,UseCookies=false}){Timeout=Timeout.InfiniteTimeSpan};
            client.DefaultRequestHeaders.UserAgent.ParseAdd("MiniCpp/"+Program.ReleaseTag.TrimStart('v'));return client;
        }
        static void ValidateResponse(HttpResponseMessage response){
            response.EnsureSuccessStatusCode();var uri=response.RequestMessage.RequestUri;
            if(uri.Scheme!="https"||!(uri.Host=="github.com"||uri.Host.EndsWith(".githubusercontent.com",StringComparison.OrdinalIgnoreCase)))throw new IOException("更新下载地址不受支持。");
        }
        internal static void Validate(UpdateDescriptor descriptor,string tag){
            var release=ReleaseVersion.Parse(tag);if(release==null)throw new InvalidDataException("更新版本无效。");
            string version=tag.TrimStart('v'),channel=release.IsPrerelease?"Dev":"Rev";
            string name="MiniCpp-"+channel+"-v"+version.Replace("-dev","")+"-win-x64-Update.zip";
            if(descriptor==null||descriptor.Version!=version||descriptor.File!=name||descriptor.Size<=0||descriptor.Size>Maximum||!System.Text.RegularExpressions.Regex.IsMatch(descriptor.Hash??"",@"^[0-9a-f]{64}$"))throw new InvalidDataException("更新包信息无效。");
        }
        internal static async Task<PreparedUpdate> PrepareAsync(ReleaseVersion release,IProgress<DownloadProgress> progress,CancellationToken token){
            using(var client=Client())return await PrepareWithClientAsync(release,progress,token,client).ConfigureAwait(false);
        }
        internal static async Task<PreparedUpdate> PrepareWithClientAsync(ReleaseVersion release,IProgress<DownloadProgress> progress,CancellationToken token,HttpClient client){
            string version=release.Tag.TrimStart('v'),prefix=GitHubUpdates.Releases+"/download/"+Uri.EscapeDataString(release.Tag)+"/";
            using(var timeout=CancellationTokenSource.CreateLinkedTokenSource(token)){
                timeout.CancelAfter(TimeSpan.FromMinutes(10));var cancel=timeout.Token;UpdateDescriptor descriptor;
                using(var response=await client.GetAsync(prefix+"update.json",HttpCompletionOption.ResponseHeadersRead,cancel).ConfigureAwait(false)){
                    ValidateResponse(response);
                    using(var data=await ReadBounded(response,65536,cancel).ConfigureAwait(false))descriptor=(UpdateDescriptor)new DataContractJsonSerializer(typeof(UpdateDescriptor)).ReadObject(data);
                }
                Validate(descriptor,release.Tag);
                string toolchain=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"w64devkit","VERSION.txt");
                if(!File.Exists(toolchain)||File.ReadAllText(toolchain).Trim()!=descriptor.Toolchain)throw new IOException("当前工具链与此更新包不匹配，请从发布页下载完整安装包或免安装版。");
                string work=Path.Combine(Native.DataDir,"updates",Guid.NewGuid().ToString("N"));System.IO.Directory.CreateDirectory(work);
                string zip=Path.Combine(work,"update.zip");
                try {
                    using(var response=await client.GetAsync(prefix+Uri.EscapeDataString(descriptor.File),HttpCompletionOption.ResponseHeadersRead,cancel).ConfigureAwait(false)){
                        ValidateResponse(response);
                        if(response.Content.Headers.ContentLength.HasValue&&response.Content.Headers.ContentLength.Value!=descriptor.Size)throw new IOException("更新文件大小不匹配。");
                        using(var input=await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                        using(var output=new FileStream(zip,FileMode.CreateNew,FileAccess.Write,FileShare.None,65536,true)){
                            var buffer=new byte[65536];long received=0;int count;
                            while((count=await input.ReadAsync(buffer,0,buffer.Length,cancel).ConfigureAwait(false))!=0){
                                received+=count;if(received>descriptor.Size)throw new IOException("更新文件超过声明大小。");
                                await output.WriteAsync(buffer,0,count,cancel).ConfigureAwait(false);
                                if(progress!=null)progress.Report(new DownloadProgress{Received=received,Total=descriptor.Size});
                            }
                            if(received!=descriptor.Size)throw new IOException("更新文件下载不完整。");
                        }
                    }
                    await Task.Run(()=>{
                        cancel.ThrowIfCancellationRequested();
                        if(UpdatePackage.HashFile(zip)!=descriptor.Hash)throw new IOException("更新包 SHA-256 校验失败，请重新下载。");
                        UpdatePackage.Extract(zip,Path.Combine(work,"verified"),version);
                        cancel.ThrowIfCancellationRequested();
                    },cancel).ConfigureAwait(false);
                    cancel.ThrowIfCancellationRequested();return new PreparedUpdate{Directory=work,Version=version,Hash=descriptor.Hash};
                }catch{if(File.Exists(zip))File.Delete(zip);throw;}
            }
        }
        static async Task<MemoryStream> ReadBounded(HttpResponseMessage response,int maximum,CancellationToken token){
            var result=new MemoryStream();
            try{using(var input=await response.Content.ReadAsStreamAsync().ConfigureAwait(false)){
                var buffer=new byte[8192];int count;while((count=await input.ReadAsync(buffer,0,buffer.Length,token).ConfigureAwait(false))!=0){if(result.Length+count>maximum)throw new IOException("更新信息过大。");result.Write(buffer,0,count);}
            }result.Position=0;return result;}catch{result.Dispose();throw;}
        }
        internal static void Launch(PreparedUpdate update,string[] sources){
            string target=AppDomain.CurrentDomain.BaseDirectory;
            int currentId;using(var current=Process.GetCurrentProcess())currentId=current.Id;
            foreach(var process in Process.GetProcessesByName("MiniCpp"))using(process){
                if(process.Id==currentId)continue;
                try{if(string.Equals(process.MainModule.FileName,Path.Combine(target,"MiniCpp.exe"),StringComparison.OrdinalIgnoreCase))throw new IOException("请先关闭其他 Mini C++ 窗口，再进行更新。");}catch(System.ComponentModel.Win32Exception){}
            }
            StartHelper(update.Directory,target,update,sources);
        }
        internal static bool RecoverAtStartup(){
            string target=AppDomain.CurrentDomain.BaseDirectory;
            if(!File.Exists(Path.Combine(target,UpdateTransaction.JournalName)))return false;
            string work=Path.Combine(Native.DataDir,"updates",Guid.NewGuid().ToString("N"));System.IO.Directory.CreateDirectory(work);
            StartHelper(work,target,null,new string[0]);return true;
        }
        static void StartHelper(string work,string target,PreparedUpdate update,string[] sources){
            string helper=Path.Combine(work,"MiniCpp.Updater.exe");
            File.Copy(Path.Combine(target,"MiniCpp.Updater.exe"),helper,true);
            using(var process=Process.GetCurrentProcess()){
                var request=new XElement("Update",new XAttribute("target",target),new XAttribute("pid",process.Id),new XAttribute("started",process.StartTime.ToUniversalTime().Ticks));
                if(update==null)request.SetAttributeValue("recover",true);
                else{request.SetAttributeValue("version",update.Version);request.SetAttributeValue("sha256",update.Hash);}
                foreach(string source in sources)request.Add(new XElement("Source",source));
                string path=Path.Combine(work,"request.xml");request.Save(path);
                Process.Start(new ProcessStartInfo(helper,Native.Quote(path)){UseShellExecute=false,CreateNoWindow=true,WorkingDirectory=work});
            }
        }
    }
}
