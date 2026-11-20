using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace MiniCpp {
    internal sealed partial class MainForm {
        bool workspaceReady, positioning, dragged, fittingTools;
        ToolStripMenuItem outputMenu;
        ToolStripStatusLabel restoreOutput;
        ToolStripButton foldOutput;
        readonly List<int> issueStarts=new List<int>();
        List<Diagnostic> shownIssues;

        int Px(int logical){return Math.Max(1,(int)Math.Round(logical*DeviceDpi/96.0));}
        void SizeIconButtons(ToolStrip strip,int width,int height){foreach(ToolStripButton b in strip.Items.OfType<ToolStripButton>())if(b.DisplayStyle==ToolStripItemDisplayStyle.Image)b.Size=new Size(Px(width),Px(height));}
        void SetupWorkspace(StatusStrip foot){
            foldOutput=Ui.Button("chevron-down","收起底部面板 · Ctrl+J",ToggleOutput);foldOutput.Alignment=ToolStripItemAlignment.Right;outputTabs.Items.Add(foldOutput);
            restoreOutput=new ToolStripStatusLabel{IsLink=true,DisplayStyle=ToolStripItemDisplayStyle.Image,Image=Ui.Art("chevron-down"),AccessibleName="展开底部面板",ToolTipText="展开底部面板 · Ctrl+J",Visible=false};
            var up=new Bitmap(restoreOutput.Image);up.RotateFlip(RotateFlipType.Rotate180FlipNone);restoreOutput.Image=up;foot.Disposed+=(s,e)=>up.Dispose();restoreOutput.Click+=(s,e)=>ToggleOutput();foot.Items.Add(restoreOutput);
            horizontal.FixedPanel=FixedPanel.Panel2;
            horizontal.SplitterMoving+=(s,e)=>dragged=true;
            horizontal.SplitterMoved+=(s,e)=>{if(workspaceReady&&!positioning&&dragged&&!horizontal.Panel2Collapsed){int space=horizontal.ClientSize.Height-horizontal.SplitterWidth;if(space>0)prefs.OutputPercent=Math.Max(1,Math.Min(99,(int)Math.Round(100.0*horizontal.Panel2.Height/space)));}dragged=false;};
            horizontal.SizeChanged+=(s,e)=>LayoutOutput();tools.SizeChanged+=(s,e)=>FitToolbar();
            horizontal.Paint+=(s,e)=>{if(horizontal.Panel2Collapsed)return;var r=horizontal.SplitterRectangle;using(var pen=new Pen(Color.FromArgb(163,169,174),Px(2)))e.Graphics.DrawLine(pen,r.Width/2-Px(17),r.Top+r.Height/2,r.Width/2+Px(17),r.Top+r.Height/2);};
            DpiChanged+=(s,e)=>{if(workspaceReady)BeginInvoke((MethodInvoker)ApplyMetrics);};
        }
        void ApplyMetrics(){if(!workspaceReady||closing)return;positioning=true;
            try{int icon=prefs.LargeTools?32:26,hit=prefs.LargeTools?48:40;
                tools.SuspendLayout();tools.ImageScalingSize=new Size(Px(icon),Px(icon));tools.Height=Px(hit+6);tools.Padding=new Padding(Px(5),Px(2),Px(5),Px(2));SizeIconButtons(tools,hit,hit);
                language.Width=Px(94);tools.ResumeLayout();FitToolbar();
                tabs.ImageScalingSize=new Size(Px(19),Px(19));tabs.Height=Px(36);SizeIconButtons(tabs,32,28);
                outputTabs.ImageScalingSize=new Size(Px(19),Px(19));outputTabs.Height=Px(34);SizeIconButtons(outputTabs,36,29);output.Padding=new Padding(Px(10),Px(5),Px(10),Px(4));
                if(restoreOutput.Owner!=null)restoreOutput.Owner.ImageScalingSize=new Size(Px(19),Px(19));
                horizontal.SplitterWidth=Px(8);vertical.SplitterWidth=Px(6);
                foreach(var d in docs)SizeEditor(d);
                foreach(var strip in vertical.Panel2.Controls.OfType<ToolStrip>()){strip.ImageScalingSize=new Size(Px(22),Px(22));strip.Height=Px(38);SizeIconButtons(strip,36,32);}
            }finally{positioning=false;}LayoutOutput();}
        void SizeEditor(SourceDocument d){d.Edit.Margins[0].Width=Px(20);d.Edit.Margins[1].Width=Px(16);d.Edit.Margins[2].Width=Px(44);}
        static bool BuildIcon(string name){return name=="compile"||name=="run"||name=="compile-and-run"||name=="rebuild";}
        void FitToolbar(){if(!workspaceReady||fittingTools)return;fittingTools=true;
            try{int hit=Px(prefs.LargeTools?48:40);bool narrow=tools.ClientSize.Width<13*hit+Px(210);bool small=tools.ClientSize.Width<7*hit+Px(205);
                tools.SuspendLayout();foreach(ToolStripItem item in tools.Items){if(item is ToolStripSeparator){item.Available=!narrow;continue;}
                    if(item==language||item==optimize||BuildIcon(item.Name))item.Overflow=ToolStripItemOverflow.Never;
                    else if(narrow&&(small||item.Name!="save"&&item.Name!="folder-open"))item.Overflow=ToolStripItemOverflow.Always;
                    else item.Overflow=ToolStripItemOverflow.AsNeeded;
                }tools.ResumeLayout();
            }finally{fittingTools=false;}}
        void LayoutOutput(){if(!workspaceReady||positioning||horizontal.Panel2Collapsed||WindowState==FormWindowState.Minimized)return;
            int space=horizontal.ClientSize.Height-horizontal.SplitterWidth;if(space<30)return;positioning=true;
            try{int top=Math.Min(Px(100),space/3),bottom=Math.Min(Px(86),space/3);
                horizontal.Panel1MinSize=top;horizontal.Panel2MinSize=bottom;
                int desired=space*(100-prefs.OutputPercent)/100;
                horizontal.SplitterDistance=Math.Max(top,Math.Min(space-bottom,desired));
            }finally{positioning=false;}}
        void SetOutputVisible(bool visible,bool remember){if(remember){prefs.OutputCollapsed=!visible;prefs.Save();}horizontal.Panel2Collapsed=!visible;restoreOutput.Visible=!visible;if(visible)LayoutOutput();else if(current!=null)current.Edit.Focus();}
        void ToggleOutput(){SetOutputVisible(horizontal.Panel2Collapsed,true);}
        void OpenView(int index){SetOutputVisible(true,true);View(index);}

        void SetupIssues(){issues.Dock=DockStyle.Fill;issues.BorderStyle=BorderStyle.None;issues.BackColor=Color.White;issues.Font=Ui.TextFont;issues.WordWrap=true;issues.ReadOnly=true;issues.DetectUrls=false;issues.AccessibleName="编译问题与中文修改建议";
            issues.DoubleClick+=(s,e)=>JumpIssue(true);issues.KeyDown+=(s,e)=>{if(e.KeyCode==Keys.Enter){JumpIssue(true);e.SuppressKeyPress=true;}};
            issues.MouseDown+=(s,e)=>{if(e.Button==MouseButtons.Right)issues.Select(issues.GetCharIndexFromPosition(e.Location),0);};
            var ctx=new ContextMenuStrip();ctx.Items.Add("定位建议检查处",null,(s,e)=>JumpIssue(true));ctx.Items.Add("定位编译器报告位置",null,(s,e)=>JumpIssue(false));issues.ContextMenuStrip=ctx;
        }
        void IssueText(string text,Color color){issues.SelectionStart=issues.TextLength;issues.SelectionColor=color;issues.AppendText(text);}
        void RenderIssues(List<Diagnostic> problems){if(shownIssues==problems&&issueStarts.Count==problems.Count)return;shownIssues=problems;issues.Clear();issueStarts.Clear();
            foreach(var d in problems){issueStarts.Add(issues.TextLength);int line=d.HintLine>0?d.HintLine:d.Line,col=d.HintColumn>0?d.HintColumn:d.Column;
                string level=d.Severity=="note"?"说明":d.Severity=="warning"?"警告":"错误";
                string loc=line>0?Path.GetFileName(d.File)+" · 第 "+line+" 行"+(col>0?"，第 "+col+" 列":""):"";
                IssueText(level+"    "+loc+"\n",level=="错误"?Color.FromArgb(145,54,52):Ui.Accent);
                if(!string.IsNullOrEmpty(d.Suggestion))IssueText(d.Suggestion+"\n",Ui.Ink);
                IssueText((d.Message.StartsWith("U+",StringComparison.Ordinal)?"字符: ":"GCC: ")+d.Message+"\n\n",Ui.Muted);
            }issues.Select(0,0);issues.ScrollToCaret();}
    }
}
