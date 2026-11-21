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
        public string Tag,Notes; Version number; string[] suffix;
        public static ReleaseVersion Parse(string tag){var m=Regex.Match(tag??"",@"^v?(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)(?:-([0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*))?(?:\+[0-9A-Za-z.-]+)?$");Version v;if(!m.Success||!Version.TryParse(string.Join(".",m.Groups[1].Value,m.Groups[2].Value,m.Groups[3].Value),out v))return null;return new ReleaseVersion{Tag=tag,number=v,suffix=m.Groups[4].Success?m.Groups[4].Value.Split('.'):new string[0]};}
        public int CompareTo(ReleaseVersion b){int c=number.CompareTo(b.number);if(c!=0)return c;if(suffix.Length==0||b.suffix.Length==0)return (b.suffix.Length>0?1:0)-(suffix.Length>0?1:0);
            for(int i=0;i<Math.Min(suffix.Length,b.suffix.Length);i++){string x=suffix[i],y=b.suffix[i];bool nx=Regex.IsMatch(x,"^[0-9]+$"),ny=Regex.IsMatch(y,"^[0-9]+$");c=nx!=ny?(nx?-1:1):nx&&x.Length!=y.Length?x.Length.CompareTo(y.Length):string.CompareOrdinal(x,y);if(c!=0)return c;}return suffix.Length.CompareTo(b.suffix.Length);}
    }
    [DataContract] internal sealed class GitRelease {
        [DataMember(Name="tag_name")]public string Tag{get;set;}
        [DataMember(Name="body")]public string Notes{get;set;}
        [DataMember(Name="draft")]public bool Draft{get;set;}
    }
    internal static class GitHubUpdates {
        internal const string Releases="https://github.com/SakuraEdge/MiniCpp/releases";
        internal static async Task<ReleaseVersion> Latest(){using(var h=new HttpClientHandler{AllowAutoRedirect=false})using(var http=new HttpClient(h){Timeout=TimeSpan.FromSeconds(15),MaxResponseContentBufferSize=8*1024*1024}){
            http.DefaultRequestHeaders.UserAgent.ParseAdd("MiniCpp/1.0.2");http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
            var reader=new DataContractJsonSerializer(typeof(GitRelease[]));ReleaseVersion best=null;
            for(int page=1;page<=20;page++){using(var r=await http.GetAsync("https://api.github.com/repos/SakuraEdge/MiniCpp/releases?per_page=30&page="+page).ConfigureAwait(false)){
                if(r.StatusCode==HttpStatusCode.NotFound||r.StatusCode==HttpStatusCode.Unauthorized)throw new IOException("无法读取公开发布列表，请稍后重试。");
                if(r.StatusCode==HttpStatusCode.Forbidden||(int)r.StatusCode==429)throw new IOException("GitHub 拒绝访问或已达到请求限额，请检查仓库权限或稍后重试。");
                if(!r.IsSuccessStatusCode)throw new IOException("GitHub 请求未成功（HTTP "+(int)r.StatusCode+"）。");
                using(var data=await r.Content.ReadAsStreamAsync().ConfigureAwait(false)){var items=(GitRelease[])reader.ReadObject(data);foreach(var item in items){if(item.Draft)continue;var v=ReleaseVersion.Parse(item.Tag);if(v!=null&&(best==null||v.CompareTo(best)>0)){v.Notes=item.Notes;best=v;}}if(items.Length<30)return best;}
            }}throw new IOException("发布列表未读取完整，请前往 GitHub 查看。");
        }}
    }
    internal sealed partial class MainForm {
        bool checkingUpdates;
        async void CheckForUpdates(){if(checkingUpdates||closing)return;checkingUpdates=true;try{Say("正在检查 GitHub 更新…");var latest=await GitHubUpdates.Latest();
            if(closing)return;if(latest==null){Say("未找到可识别的发布版本");MessageBox.Show(this,"仓库还没有可识别的发布版本。","检查更新");return;}
            int order=latest.CompareTo(ReleaseVersion.Parse(Program.ReleaseTag));string summary=order>0?"发现新版本":order==0?"已是最新版本":"本机版本高于已发布版本";Say(summary);ShowReleaseNotes(latest,summary);
        }
        catch(Exception e){if(!closing){Say("无法检查更新");Error(e is IOException?e.Message:"无法完成 GitHub 更新检查，请检查网络后重试。");}}finally{checkingUpdates=false;}}
        static void OpenReleases(string suffix){Process.Start(new ProcessStartInfo(GitHubUpdates.Releases+suffix){UseShellExecute=true});}
        void ShowReleaseNotes(ReleaseVersion latest,string summary){using(var f=new Form{Text="检查更新 · "+summary,Font=Ui.TextFont,AutoScaleDimensions=new SizeF(96,96),AutoScaleMode=AutoScaleMode.Dpi,ClientSize=new Size(520,330),MinimumSize=new Size(350,220),StartPosition=FormStartPosition.CenterParent,ShowInTaskbar=false,MinimizeBox=false,Padding=new Padding(14)}){
            var meta=new Label{Dock=DockStyle.Top,AutoSize=true,UseMnemonic=false,Text="当前："+Program.DisplayVersion+"    GitHub："+latest.Tag+"\n\n更新内容"};
            var notes=new RichTextBox{Dock=DockStyle.Fill,ReadOnly=true,DetectUrls=false,BorderStyle=BorderStyle.None,Font=Ui.TextFont,WordWrap=true,ScrollBars=RichTextBoxScrollBars.Vertical,AccessibleName="更新内容",Text=string.IsNullOrWhiteSpace(latest.Notes)?"此版本未填写更新说明。":latest.Notes.Trim()};
            var controls=new FlowLayoutPanel{AutoSize=true,Dock=DockStyle.Bottom,FlowDirection=FlowDirection.RightToLeft};var close=new Button{Text="关闭",AutoSize=true,DialogResult=DialogResult.Cancel};var page=new Button{Text="打开发布页",AutoSize=true,DialogResult=DialogResult.OK};controls.Controls.AddRange(new Control[]{close,page});f.Controls.Add(notes);f.Controls.Add(meta);f.Controls.Add(controls);f.CancelButton=close;Theme.Dialog(f);f.HandleCreated+=(s,e)=>Theme.CaptionTheme(f,prefs.DarkTheme);if(f.ShowDialog(this)==DialogResult.OK)OpenReleases("/tag/"+Uri.EscapeDataString(latest.Tag));
        }}
    }
}
