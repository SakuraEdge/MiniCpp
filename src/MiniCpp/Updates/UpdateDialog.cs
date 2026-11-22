using System;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

namespace MiniCpp {
    internal sealed class UpdateDialog:Form {
        readonly Label headline=new Label{AutoSize=true,Text="正在检查更新",Font=new Font(Ui.TextFont.FontFamily,18,FontStyle.Bold)};
        readonly Label versions=new Label{AutoSize=true,Text="当前版本  "+Program.DisplayVersion};
        readonly Label status=new Label{AutoSize=true,MaximumSize=new Size(600,0)};
        readonly RichTextBox notes=new RichTextBox{Dock=DockStyle.Fill,ReadOnly=true,DetectUrls=false,BorderStyle=BorderStyle.None,Font=Ui.TextFont};
        readonly ProgressBar progress=new ProgressBar{Dock=DockStyle.Fill,Height=8,Style=ProgressBarStyle.Marquee};
        readonly Button primary=new Button{Text="检查中…",AutoSize=true,Enabled=false};
        readonly Button dismiss=new Button{Text="关闭",AutoSize=true};
        readonly Button page=new Button{Text="发布页",AutoSize=true};
        readonly CancellationTokenSource cancellation=new CancellationTokenSource();
        ReleaseVersion latest;bool busy,checking;
        internal PreparedUpdate Prepared;

        internal UpdateDialog(){
            Text="Mini C++ 更新";Font=Ui.TextFont;AutoScaleDimensions=new SizeF(96,96);AutoScaleMode=AutoScaleMode.Dpi;
            ClientSize=new Size(640,470);MinimumSize=new Size(510,370);StartPosition=FormStartPosition.CenterParent;MinimizeBox=false;ShowInTaskbar=false;
            var layout=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=6,Padding=new Padding(24)};
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,10));layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            headline.Margin=new Padding(0,0,0,8);versions.Margin=new Padding(0,0,0,20);notes.Margin=new Padding(0,0,0,16);status.Margin=new Padding(0,8,0,10);
            layout.Controls.Add(headline,0,0);layout.Controls.Add(versions,0,1);layout.Controls.Add(notes,0,2);layout.Controls.Add(status,0,3);layout.Controls.Add(progress,0,4);
            var buttons=new FlowLayoutPanel{AutoSize=true,Dock=DockStyle.Fill,FlowDirection=FlowDirection.RightToLeft,Margin=new Padding(0,16,0,0)};
            buttons.Controls.Add(primary);buttons.Controls.Add(dismiss);buttons.Controls.Add(page);layout.Controls.Add(buttons,0,5);Controls.Add(layout);
            Theme.Dialog(this);notes.BackColor=Theme.Current.Paper;headline.ForeColor=Theme.Current.Accent;status.ForeColor=Theme.Current.Muted;
            primary.Click+=(s,e)=>{if(Prepared!=null){DialogResult=DialogResult.OK;Close();}else if(latest!=null&&latest.CompareTo(ReleaseVersion.Parse(Program.ReleaseTag))>0)Download();else Check();};
            dismiss.Click+=(s,e)=>{if(busy){cancellation.Cancel();dismiss.Enabled=false;}else Close();};
            page.Click+=(s,e)=>{try{Process.Start(new ProcessStartInfo(GitHubUpdates.Releases+(latest==null?"":"/tag/"+Uri.EscapeDataString(latest.Tag))){UseShellExecute=true});}catch(Exception ex){status.Text=ex.Message;}};
            Shown+=(s,e)=>Check();HandleCreated+=(s,e)=>Theme.CaptionTheme(this,Theme.Current.Amount>=.5);
            FormClosing+=(s,e)=>{if(busy){e.Cancel=true;cancellation.Cancel();}else cancellation.Cancel();};
        }
        async void Check(){
            if(checking)return;checking=true;primary.Enabled=false;progress.Visible=true;progress.Style=ProgressBarStyle.Marquee;headline.Text="正在检查更新";status.Text="正在连接 GitHub…";
            try{
                var release=await GitHubUpdates.Latest();if(IsDisposed)return;latest=release;
                if(latest==null)throw new InvalidOperationException("尚未找到发布版本。");
                ShowRelease(latest);
            }catch(Exception ex){if(IsDisposed)return;headline.Text="暂时无法检查更新";status.Text=ex.Message;primary.Text="重试";latest=null;}
            finally{checking=false;if(!IsDisposed){progress.Visible=false;primary.Enabled=true;}}
        }
        internal void ShowRelease(ReleaseVersion release){
            latest=release;int order=latest.CompareTo(ReleaseVersion.Parse(Program.ReleaseTag));bool newer=order>0;
            headline.Text=newer?"发现新版本":order<0?"本机版本高于当前发布":"当前已是最新版本";
            if(!newer&&latest.FromRecentFeed)headline.Text="近期发布中未发现更新";
            versions.Text="当前  "+Program.DisplayVersion+"      GitHub  "+latest.Tag;
            notes.Text=string.IsNullOrWhiteSpace(latest.Notes)?"此版本未填写更新说明。":latest.Notes.Trim();
            status.Text=newer?"下载后将保存文件并重启完成更新。":"上次检查  "+DateTime.Now.ToString("HH:mm");primary.Text=newer?"下载并更新":"重新检查";
            progress.Visible=false;primary.Enabled=true;
        }
        async void Download(){
            if(busy)return;busy=true;primary.Enabled=false;dismiss.Text="取消下载";progress.Visible=true;progress.Style=ProgressBarStyle.Continuous;progress.Value=0;headline.Text="正在下载更新";
            try{
                var reporter=new Progress<DownloadProgress>(p=>{if(IsDisposed)return;progress.Value=(int)Math.Min(100,p.Received*100/p.Total);status.Text=string.Format("{0:F1} / {1:F1} MB",p.Received/1048576.0,p.Total/1048576.0);});
                Prepared=await UpdateDownload.PrepareAsync(latest,reporter,cancellation.Token);busy=false;DialogResult=DialogResult.OK;Close();
            }catch(OperationCanceledException){busy=false;if(cancellation.IsCancellationRequested)Close();else{headline.Text="下载超时";status.Text="请重试下载。";primary.Text="重试下载";primary.Enabled=true;dismiss.Text="关闭";progress.Visible=false;}}
            catch(Exception ex){busy=false;headline.Text="更新下载未完成";status.Text=ex.Message;primary.Text="重试下载";primary.Enabled=true;dismiss.Text="关闭";progress.Visible=false;}
        }
        protected override void Dispose(bool disposing){if(disposing){cancellation.Dispose();headline.Font.Dispose();}base.Dispose(disposing);}
    }
}
