using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MiniCpp {
    internal sealed class ReleaseVersion : IComparable<ReleaseVersion> {
        public string Tag; Version number; string[] suffix;
        public static ReleaseVersion Parse(string tag){var m=Regex.Match(tag??"",@"^v?(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)(?:-([0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*))?(?:\+[0-9A-Za-z.-]+)?$");Version v;if(!m.Success||!Version.TryParse(string.Join(".",m.Groups[1].Value,m.Groups[2].Value,m.Groups[3].Value),out v))return null;return new ReleaseVersion{Tag=tag,number=v,suffix=m.Groups[4].Success?m.Groups[4].Value.Split('.'):new string[0]};}
        public int CompareTo(ReleaseVersion b){int c=number.CompareTo(b.number);if(c!=0)return c;if(suffix.Length==0||b.suffix.Length==0)return (b.suffix.Length>0?1:0)-(suffix.Length>0?1:0);
            for(int i=0;i<Math.Min(suffix.Length,b.suffix.Length);i++){string x=suffix[i],y=b.suffix[i];bool nx=Regex.IsMatch(x,"^[0-9]+$"),ny=Regex.IsMatch(y,"^[0-9]+$");c=nx!=ny?(nx?-1:1):nx&&x.Length!=y.Length?x.Length.CompareTo(y.Length):string.CompareOrdinal(x,y);if(c!=0)return c;}return suffix.Length.CompareTo(b.suffix.Length);}
    }
    [DataContract] internal sealed class GitRelease {
        [DataMember(Name="tag_name")]public string Tag{get;set;}
        [DataMember(Name="draft")]public bool Draft{get;set;}
    }
    internal sealed class GitHubAccessException:Exception {}
    internal static class GitHubUpdates {
        internal const string Releases="https://github.com/SakuraEdge/MiniCpp/releases";
        internal static async Task<ReleaseVersion> Latest(string token){using(var h=new HttpClientHandler{AllowAutoRedirect=false})using(var http=new HttpClient(h){Timeout=TimeSpan.FromSeconds(15),MaxResponseContentBufferSize=8*1024*1024}){
            http.DefaultRequestHeaders.UserAgent.ParseAdd("MiniCpp/1.0");http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");if(!string.IsNullOrEmpty(token))http.DefaultRequestHeaders.Authorization=new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer",token);
            var reader=new DataContractJsonSerializer(typeof(GitRelease[]));ReleaseVersion best=null;
            for(int page=1;page<=20;page++){using(var r=await http.GetAsync("https://api.github.com/repos/SakuraEdge/MiniCpp/releases?per_page=30&page="+page).ConfigureAwait(false)){
                if(r.StatusCode==HttpStatusCode.NotFound||r.StatusCode==HttpStatusCode.Unauthorized)throw new GitHubAccessException();
                if(r.StatusCode==HttpStatusCode.Forbidden||(int)r.StatusCode==429)throw new IOException("GitHub 拒绝访问或已达到请求限额，请检查仓库权限或稍后重试。");
                if(!r.IsSuccessStatusCode)throw new IOException("GitHub 请求未成功（HTTP "+(int)r.StatusCode+"）。");
                using(var data=await r.Content.ReadAsStreamAsync().ConfigureAwait(false)){var items=(GitRelease[])reader.ReadObject(data);foreach(var item in items){if(item.Draft)continue;var v=ReleaseVersion.Parse(item.Tag);if(v!=null&&(best==null||v.CompareTo(best)>0))best=v;}if(items.Length<30)return best;}
            }}throw new IOException("发布列表未读取完整，请前往 GitHub 查看。");
        }}
    }
    internal sealed partial class MainForm {
        bool checkingUpdates;
        async void CheckForUpdates(){if(checkingUpdates||closing)return;checkingUpdates=true;try{Say("正在检查 GitHub 更新…");var token=Environment.GetEnvironmentVariable("MINICPP_GITHUB_TOKEN");ReleaseVersion latest=null;bool retry=false;
            try{latest=await GitHubUpdates.Latest(token);}catch(GitHubAccessException){retry=true;}
            if(retry){if(closing)return;token=AskForReleaseAccess();if(string.IsNullOrWhiteSpace(token)){Say("未完成更新检查");return;}latest=await GitHubUpdates.Latest(token.Trim());}
            if(closing)return;if(latest==null){Say("未找到可识别的发布版本");MessageBox.Show(this,"仓库还没有可识别的发布版本。","检查更新");return;}
            if(latest.CompareTo(ReleaseVersion.Parse(Program.ReleaseTag))<=0){Say("未发现新版本");MessageBox.Show(this,"当前版本："+Program.DisplayVersion+"\n没有比当前版本更新的发布。","检查更新");return;}
            Say("发现新版本："+latest.Tag);if(MessageBox.Show(this,"当前版本："+Program.DisplayVersion+"\n可用版本："+latest.Tag+"\n\n打开 GitHub 发布页？","发现新版本",MessageBoxButtons.YesNo,MessageBoxIcon.Information)==DialogResult.Yes)OpenReleases("/tag/"+Uri.EscapeDataString(latest.Tag));
        }catch(GitHubAccessException){if(!closing){Say("无法检查更新");Error("无法访问发布列表。请确认令牌具有此私有仓库的 Contents 只读权限。");}}
        catch(Exception e){if(!closing){Say("无法检查更新");Error(e is IOException?e.Message:"无法完成 GitHub 更新检查，请检查网络后重试。");}}finally{checkingUpdates=false;}}
        static void OpenReleases(string suffix){Process.Start(new ProcessStartInfo(GitHubUpdates.Releases+suffix){UseShellExecute=true});}
        string AskForReleaseAccess(){using(var f=new Form{Text="访问 GitHub 发布页",Font=Ui.TextFont,AutoScaleMode=AutoScaleMode.Font,AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,FormBorderStyle=FormBorderStyle.FixedDialog,MaximizeBox=false,MinimizeBox=false,StartPosition=FormStartPosition.CenterParent,ShowInTaskbar=false}){
            var p=new FlowLayoutPanel{FlowDirection=FlowDirection.TopDown,WrapContents=false,AutoSize=true,Padding=new Padding(14)};
            p.Controls.Add(new Label{AutoSize=true,Text="此仓库需要授权。可输入此仓库的只读 GitHub 令牌，\n仅用于本次检查，不写入磁盘。"});var box=new TextBox{Width=355,UseSystemPasswordChar=true,AccessibleName="只读 GitHub 令牌"};p.Controls.Add(box);
            var buttons=new FlowLayoutPanel{AutoSize=true};var yes=new Button{Text="检查",DialogResult=DialogResult.OK};var browser=new Button{AutoSize=true,Text="在浏览器查看",DialogResult=DialogResult.Retry};var no=new Button{Text="取消",DialogResult=DialogResult.Cancel};buttons.Controls.AddRange(new Control[]{yes,browser,no});p.Controls.Add(buttons);f.Controls.Add(p);f.AcceptButton=yes;f.CancelButton=no;var result=f.ShowDialog(this);if(result==DialogResult.Retry)OpenReleases("");return result==DialogResult.OK?box.Text:null;
        }}
    }
}
