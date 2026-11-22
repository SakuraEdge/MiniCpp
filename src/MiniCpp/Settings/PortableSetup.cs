using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Windows.Forms;

namespace MiniCpp {
    internal static class PortableSetup {
        internal static bool Enabled { get { return File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"portable.mode")); } }
        internal static string StatePath {
            get {
                using(var hash=SHA256.Create()){
                    string id=BitConverter.ToString(hash.ComputeHash(Native.Utf8.GetBytes(DesktopIntegration.Executable.ToUpperInvariant()))).Replace("-","");
                    return Path.Combine(Native.DataDir,"portable",id+".initialized");
                }
            }
        }

        internal static void ShowFirstRun(IWin32Window owner){
            if(!Enabled)return;
            try {
                string path=StatePath;
                // Only one window prompts for this portable copy at a time.
                using(var mutex=new Mutex(false,@"Local\MiniCpp.PortableSetup."+Path.GetFileNameWithoutExtension(path))){
                    try { if(!mutex.WaitOne(0))return; } catch(AbandonedMutexException){}
                    try { if(!File.Exists(path))ShowOptions(owner); } finally { mutex.ReleaseMutex(); }
                }
            } catch(Exception ex){MessageBox.Show(owner,"无法保存免安装版的首次启动设置。\n"+ex.GetBaseException().Message,"Mini C++",MessageBoxButtons.OK,MessageBoxIcon.Warning);}
        }

        internal static void RememberChoice(){
            string path=StatePath;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            using(var stream=new FileStream(path,FileMode.OpenOrCreate,FileAccess.Write,FileShare.Read))stream.Flush(true);
        }

        internal static void ShowOptions(IWin32Window owner){
            using(var dialog=new PortableSetupDialog())dialog.ShowDialog(owner);
            try { RememberChoice(); }
            catch(IOException ex){MessageBox.Show(owner,ex.Message,"无法保存首次启动设置",MessageBoxButtons.OK,MessageBoxIcon.Warning);}
            catch(UnauthorizedAccessException ex){MessageBox.Show(owner,ex.Message,"无法保存首次启动设置",MessageBoxButtons.OK,MessageBoxIcon.Warning);}
        }
    }

    internal sealed class PortableSetupDialog:Form {
        readonly CheckBox desktop=new CheckBox{Text="创建桌面快捷方式",Checked=true,AutoSize=true,Name="desktopShortcut"};
        readonly CheckBox defaults=new CheckBox{Text="将 .cpp 默认打开方式设为此免安装版",AutoSize=true,Name="defaultCpp"};

        internal PortableSetupDialog(){
            Text="Mini C++ 免安装版设置";Font=Ui.TextFont;AutoScaleDimensions=new SizeF(96,96);AutoScaleMode=AutoScaleMode.Dpi;
            StartPosition=FormStartPosition.CenterParent;FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;MinimizeBox=false;ShowInTaskbar=false;
            AutoSize=true;AutoSizeMode=AutoSizeMode.GrowAndShrink;
            var body=new TableLayoutPanel{AutoSize=true,ColumnCount=1,Padding=new Padding(20)};
            body.Controls.Add(new Label{Text="当前程序",AutoSize=true,Margin=new Padding(0,0,0,6)});
            body.Controls.Add(new TextBox{Text=DesktopIntegration.Executable,ReadOnly=true,Width=460,Margin=new Padding(0,0,0,15),TabStop=false});
            desktop.Margin=new Padding(0,4,0,10);defaults.Margin=new Padding(0,4,0,6);
            body.Controls.Add(desktop);body.Controls.Add(defaults);
            body.Controls.Add(new Label{Text="Windows 会打开默认应用设置，请在那里确认 .cpp 的打开方式。\n快捷方式和文件关联使用当前解压目录。",AutoSize=true,MaximumSize=new Size(460,0),Margin=new Padding(0,0,0,12)});
            var actions=new FlowLayoutPanel{AutoSize=true,Anchor=AnchorStyles.Right,FlowDirection=FlowDirection.RightToLeft,Margin=Padding.Empty};
            var apply=new Button{Text="确定",AutoSize=true,Name="apply"};
            var skip=new Button{Text="暂不设置",AutoSize=true,DialogResult=DialogResult.Cancel,Name="skip"};
            apply.Click+=(s,e)=>ApplyChoices();actions.Controls.Add(apply);actions.Controls.Add(skip);body.Controls.Add(actions);
            Controls.Add(body);AcceptButton=apply;CancelButton=skip;Theme.Dialog(this);HandleCreated+=(s,e)=>Theme.CaptionTheme(this,Theme.Current.Amount>=.5);
        }

        void ApplyChoices(){
            var errors=new List<string>();
            if(desktop.Checked){
                try { DesktopIntegration.CreateDesktopShortcut();desktop.Checked=false; }
                catch(Exception ex){errors.Add("桌面快捷方式创建失败。\n"+ex.GetBaseException().Message);}
            }
            if(defaults.Checked){
                try { DesktopIntegration.RegisterCpp();DesktopIntegration.OpenDefaultApps();defaults.Checked=false; }
                catch(Exception ex){errors.Add("无法打开默认应用设置。\n"+ex.GetBaseException().Message);}
            }
            if(errors.Count>0){MessageBox.Show(this,string.Join("\n\n",errors),"Mini C++",MessageBoxButtons.OK,MessageBoxIcon.Warning);return;}
            DialogResult=DialogResult.OK;Close();
        }
    }
}
