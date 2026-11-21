using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

[assembly:System.Runtime.Versioning.TargetFramework(".NETFramework,Version=v4.8")]

// Exercise the compiled updater's fallback with an exhausted API quota, independent of the current IP quota.
internal static class UpdateSmoke {
    const string Releases="https://github.com/SakuraEdge/MiniCpp/releases";
    sealed class LimitedGitHub : DelegatingHandler {
        public int ApiCalls,FeedCalls;
        public bool Offline,Live;
        public LimitedGitHub() : base(new HttpClientHandler{AllowAutoRedirect=false,UseCookies=false}) {}
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage req,CancellationToken token){
            if(req.RequestUri.Host=="api.github.com"){
                ApiCalls++;var r=new HttpResponseMessage(HttpStatusCode.Forbidden);
                r.Headers.Add("X-RateLimit-Remaining","0");r.Headers.RetryAfter=new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromMinutes(5));
                return Task.FromResult(r);
            }
            if(req.RequestUri.AbsoluteUri!=Releases+".atom")throw new InvalidOperationException("Unexpected update source");
            FeedCalls++;
            if(Offline)return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
            if(Live)return base.SendAsync(req,token);
            string body="<feed xmlns='http://www.w3.org/2005/Atom'><link rel='self' href='"+Releases+".atom'/>"+
                "<entry><link rel='alternate' href='"+Releases+"/tag/v1.0.3-dev'/></entry>"+
                "<entry><link rel='alternate' href='"+Releases+"/tag/v1.0.0'/><content type='html'>&lt;p&gt;中文更新&lt;br&gt;支持 &amp;lt;vector&amp;gt;&lt;/p&gt;</content></entry></feed>";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(body,Encoding.UTF8,"application/atom+xml")});
        }
    }

    static object Check(MethodInfo method,object checker){
        var task=(Task)method.Invoke(checker,null);task.GetAwaiter().GetResult();return task.GetType().GetProperty("Result").GetValue(task,null);
    }
    public static int Main(string[] args){try{
        var asm=Assembly.LoadFrom(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"MiniCpp.exe"));
        var type=asm.GetType("MiniCpp.GitHubUpdates",true);var method=type.GetMethod("CheckAsync",BindingFlags.Instance|BindingFlags.NonPublic);
        var handler=new LimitedGitHub{Live=Array.IndexOf(args,"--live-feed")>=0};
        using(var http=new HttpClient(handler){Timeout=TimeSpan.FromSeconds(15),MaxResponseContentBufferSize=8*1024*1024}){
            var checker=Activator.CreateInstance(type,BindingFlags.Instance|BindingFlags.NonPublic,null,new object[]{http},null);
            var release=Check(method,checker);string tag=(string)release.GetType().GetField("Tag").GetValue(release),notes=(string)release.GetType().GetField("Notes").GetValue(release);
            if(!(bool)release.GetType().GetField("FromRecentFeed").GetValue(release)||string.IsNullOrEmpty(tag)||string.IsNullOrWhiteSpace(notes))throw new Exception("No release notes from feed");
            if(!handler.Live&&(tag!="v1.0.0"||notes!="中文更新\n支持 <vector>"))throw new Exception("Incorrect release channel or notes");
            handler.Offline=true;
            try{Check(method,checker);throw new Exception("An unreachable feed must not report a version");}
            catch(IOException e){if(!e.Message.Contains("503"))throw;}
            if(handler.ApiCalls!=1||handler.FeedCalls!=2)throw new Exception("API cooldown was not respected");
            Console.WriteLine("PASS: 403 -> "+(handler.Live?"live GitHub ":"")+"Atom -> "+tag+"; cooldown and offline failure. Notes: "+notes.Split('\n')[0]);
        }return 0;
    }catch(Exception e){Console.Error.WriteLine(e);return 1;}}
}
