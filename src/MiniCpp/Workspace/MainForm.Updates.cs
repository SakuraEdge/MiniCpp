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
    internal sealed partial class MainForm {
        bool checkingUpdates;
        async void CheckForUpdates(){if(checkingUpdates||closing)return;checkingUpdates=true;try{Say("正在检查 GitHub 更新…");var latest=await GitHubUpdates.Latest();
            if(closing)return;if(latest==null){Say("未找到可识别的发布版本");MessageBox.Show(this,"仓库还没有可识别的发布版本。","检查更新");return;}
            int order=latest.CompareTo(ReleaseVersion.Parse(Program.ReleaseTag));string summary=order>0?"发现新版本":latest.FromRecentFeed?"近期发布中未发现更新":order==0?"已是最新版本":"本机版本高于已发布版本";Say(summary);ShowReleaseNotes(latest,summary);
        }
        catch(Exception e){if(!closing){Say("无法检查更新");string reason=e is IOException?e.Message:"无法完成 GitHub 更新检查，请检查网络后重试。";
            if(MessageBox.Show(this,reason+"\r\n\r\n是否在浏览器中打开 GitHub 发布页？","检查更新",MessageBoxButtons.YesNo,MessageBoxIcon.Information,MessageBoxDefaultButton.Button2)==DialogResult.Yes)OpenReleases("");}}finally{checkingUpdates=false;}}
        static void OpenReleases(string suffix){Process.Start(new ProcessStartInfo(GitHubUpdates.Releases+suffix){UseShellExecute=true});}
        void ShowReleaseNotes(ReleaseVersion latest,string summary){using(var f=new Form{Text="检查更新 · "+summary,Font=Ui.TextFont,AutoScaleDimensions=new SizeF(96,96),AutoScaleMode=AutoScaleMode.Dpi,ClientSize=new Size(520,330),MinimumSize=new Size(350,220),StartPosition=FormStartPosition.CenterParent,ShowInTaskbar=false,MinimizeBox=false,Padding=new Padding(14)}){
            var meta=new Label{Dock=DockStyle.Top,AutoSize=true,UseMnemonic=false,Text="当前："+Program.DisplayVersion+"    GitHub："+latest.Tag+"\n\n更新内容"};
            var notes=new RichTextBox{Dock=DockStyle.Fill,ReadOnly=true,DetectUrls=false,BorderStyle=BorderStyle.None,Font=Ui.TextFont,WordWrap=true,ScrollBars=RichTextBoxScrollBars.Vertical,AccessibleName="更新内容",Text=string.IsNullOrWhiteSpace(latest.Notes)?"此版本未填写更新说明。":latest.Notes.Trim()};
            var controls=new FlowLayoutPanel{AutoSize=true,Dock=DockStyle.Bottom,FlowDirection=FlowDirection.RightToLeft};var close=new Button{Text="关闭",AutoSize=true,DialogResult=DialogResult.Cancel};var page=new Button{Text="打开发布页",AutoSize=true,DialogResult=DialogResult.OK};controls.Controls.AddRange(new Control[]{close,page});f.Controls.Add(notes);f.Controls.Add(meta);f.Controls.Add(controls);f.CancelButton=close;Theme.Dialog(f);f.HandleCreated+=(s,e)=>Theme.CaptionTheme(f,prefs.DarkTheme);if(f.ShowDialog(this)==DialogResult.OK)OpenReleases("/tag/"+Uri.EscapeDataString(latest.Tag));
        }}
    }
}
