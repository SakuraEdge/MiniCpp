using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;

namespace MiniCpp {
    [DataContract] internal sealed class GitRelease {
        [DataMember(Name="tag_name")] public string Tag {get;set;}
        [DataMember(Name="body")] public string Notes {get;set;}
        [DataMember(Name="draft")] public bool Draft {get;set;}
        [DataMember(Name="prerelease")] public bool Prerelease {get;set;}
    }

    internal sealed class GitHubUpdates {
        internal const string Releases="https://github.com/SakuraEdge/MiniCpp/releases";
        const string Api="https://api.github.com/repos/SakuraEdge/MiniCpp/releases";
        const int MaxBytes=8*1024*1024;
        static readonly XNamespace Atom="http://www.w3.org/2005/Atom";
        static readonly GitHubUpdates Shared=new GitHubUpdates(new HttpClient(new HttpClientHandler {
            AllowAutoRedirect=false, UseCookies=false, AutomaticDecompression=DecompressionMethods.GZip|DecompressionMethods.Deflate
        }) {Timeout=TimeSpan.FromSeconds(15),MaxResponseContentBufferSize=MaxBytes});
        readonly HttpClient http;
        readonly bool includePrereleases=ReleaseVersion.Parse(Program.ReleaseTag).IsPrerelease;
        DateTimeOffset apiRetryAt;

        internal GitHubUpdates(HttpClient http){this.http=http;}
        internal static Task<ReleaseVersion> Latest(){return Shared.CheckAsync();}

        internal async Task<ReleaseVersion> CheckAsync(){
            string apiFailure="匿名 API 配额尚未恢复";
            if(DateTimeOffset.UtcNow>=apiRetryAt){
                try{return await ReadApi().ConfigureAwait(false);}
                catch(Exception e){apiFailure=Failure(e);if(apiFailure==null)throw;}
            }
            try{return await ReadFeed().ConfigureAwait(false);}
            catch(Exception e){string feedFailure=Failure(e);if(feedFailure==null)throw;
                throw new IOException("暂时无法确认最新版本。\r\nGitHub API："+apiFailure+"\r\n公开发布订阅："+feedFailure,e);}
        }

        static string Failure(Exception e){
            if(e is OperationCanceledException)return "连接超时";
            if(e is HttpRequestException)return "网络连接失败，请检查网络或代理";
            if(e is XmlException||e is SerializationException)return "发布数据格式异常";
            if(e is InvalidDataException)return e.Message;
            return e is IOException?e.Message:null;
        }

        async Task<HttpResponseMessage> Get(string url,string accept){
            using(var request=new HttpRequestMessage(HttpMethod.Get,url)){
                request.Headers.UserAgent.ParseAdd("MiniCpp/"+Program.ReleaseTag.TrimStart('v'));
                request.Headers.Accept.ParseAdd(accept);
                return await http.SendAsync(request).ConfigureAwait(false);
            }
        }

        static string Header(HttpResponseMessage r,string name){IEnumerable<string> values;return r.Headers.TryGetValues(name,out values)?values.FirstOrDefault():null;}
        void CheckStatus(HttpResponseMessage r,bool api){
            if(r.IsSuccessStatusCode)return;
            int status=(int)r.StatusCode;
            if(api&&(status==429||status==403&&(Header(r,"X-RateLimit-Remaining")=="0"||r.Headers.RetryAfter!=null))){
                var now=DateTimeOffset.UtcNow;var retry=r.Headers.RetryAfter;long reset;
                if(retry!=null&&retry.Date.HasValue)apiRetryAt=retry.Date.Value;
                else if(retry!=null&&retry.Delta.HasValue)apiRetryAt=now+retry.Delta.Value;
                else if(long.TryParse(Header(r,"X-RateLimit-Reset"),NumberStyles.None,CultureInfo.InvariantCulture,out reset)&&reset>=0&&reset<=253402300799L)
                    apiRetryAt=DateTimeOffset.FromUnixTimeSeconds(reset);
                if(apiRetryAt<=now)apiRetryAt=now.AddMinutes(1);
                throw new IOException("匿名请求已限流（HTTP "+status+"）");
            }
            if(status==403)throw new IOException("访问被拒绝（HTTP 403）");
            throw new IOException("请求未成功（HTTP "+status+"）");
        }

        async Task<ReleaseVersion> ReadApi(){
            var reader=new DataContractJsonSerializer(typeof(GitRelease[]));ReleaseVersion best=null;
            for(int page=1;page<=20;page++){
                using(var r=await Get(Api+"?per_page=30&page="+page,"application/vnd.github+json").ConfigureAwait(false)){
                    CheckStatus(r,true);
                    using(var stream=await r.Content.ReadAsStreamAsync().ConfigureAwait(false)){
                        var items=(GitRelease[])reader.ReadObject(stream);
                        if(items==null)throw new InvalidDataException("发布列表格式异常");
                        foreach(var item in items){if(item==null||item.Draft||(!includePrereleases&&item.Prerelease))continue;var v=ReleaseVersion.Parse(item.Tag);
                            if(IsNewerCandidate(v,best)){v.Notes=item.Notes;best=v;}}
                        if(items.Length<30)return best;
                    }
                }
            }
            throw new IOException("发布列表未读取完整");
        }

        async Task<ReleaseVersion> ReadFeed(){
            using(var r=await Get(Releases+".atom","application/atom+xml").ConfigureAwait(false)){
                CheckStatus(r,false);
                using(var stream=await r.Content.ReadAsStreamAsync().ConfigureAwait(false))
                using(var reader=XmlReader.Create(stream,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=MaxBytes})){
                    var feed=XElement.Load(reader);
                    if(feed.Name!=Atom+"feed"||!feed.Elements(Atom+"link").Any(x=>(string)x.Attribute("rel")=="self"&&(string)x.Attribute("href")==Releases+".atom"))
                        throw new InvalidDataException("未收到 Mini C++ 的发布订阅");
                    var prefix=Releases+"/tag/";ReleaseVersion best=null;
                    foreach(var entry in feed.Elements(Atom+"entry")){
                        var href=entry.Elements(Atom+"link").Where(x=>(string)x.Attribute("rel")=="alternate").Select(x=>(string)x.Attribute("href"))
                            .FirstOrDefault(x=>x!=null&&x.StartsWith(prefix,StringComparison.Ordinal));
                        if(href==null)continue;var v=ReleaseVersion.Parse(Uri.UnescapeDataString(href.Substring(prefix.Length)));
                        if(!IsNewerCandidate(v,best))continue;
                        var content=entry.Element(Atom+"content")??entry.Element(Atom+"summary");
                        if(content!=null)v.Notes=(string)content.Attribute("type")=="html"?PlainNotes(content.Value):content.Value;
                        v.FromRecentFeed=true;best=v;
                    }
                    if(best==null)throw new InvalidDataException("近期订阅中没有本发行通道的可识别版本");
                    return best;
                }
            }
        }

        bool IsNewerCandidate(ReleaseVersion candidate,ReleaseVersion best){
            return candidate!=null&&(includePrereleases||!candidate.IsPrerelease)&&(best==null||candidate.CompareTo(best)>0);
        }

        // GitHub puts escaped HTML in Atom content. Convert it to text, never render it as HTML.
        static string PlainNotes(string html){
            string text=Regex.Replace(html,@"<br\b[^<>]*>|</(?:p|li|h[1-6]|pre|div)\s*>","\n",RegexOptions.IgnoreCase);
            text=Regex.Replace(text,@"<li\b[^<>]*>","• ",RegexOptions.IgnoreCase);
            text=WebUtility.HtmlDecode(Regex.Replace(text,@"</?[A-Za-z][^<>]*>","")).Replace("\r\n","\n");
            return Regex.Replace(text,@"\n{3,}","\n\n").Trim();
        }
    }
}
