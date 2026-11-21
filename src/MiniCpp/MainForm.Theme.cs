using System;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace MiniCpp {
    internal sealed partial class MainForm {
        readonly Timer themeTimer=new Timer{Interval=15};
        readonly Stopwatch themeWatch=new Stopwatch();
        readonly FlatStrip paletteRenderer=new FlatStrip();
        ToolStripButton themeButton;
        double fadeFrom,fadeTo;

        void SetupTheme(){
            themeButton=Ui.Button("contrast","切换主题",ToggleTheme);
            themeButton.Alignment=ToolStripItemAlignment.Right;themeButton.Overflow=ToolStripItemOverflow.Never;tools.Items.Add(themeButton);
            themeTimer.Tick+=(s,e)=>AdvanceTheme();
            UpdateThemeAction();ApplyPalette(new Theme(prefs.DarkTheme?1:0),true);
            HandleCreated+=(s,e)=>Theme.CaptionTheme(this,prefs.DarkTheme);
        }
        void UpdateThemeAction(){themeButton.Checked=prefs.DarkTheme;themeButton.ToolTipText=themeButton.AccessibleName=prefs.DarkTheme?"切换到亮色主题":"切换到暗色主题";}
        void ToggleTheme(){
            if(closing)return;
            fadeFrom=Theme.Current.Amount;prefs.DarkTheme=!prefs.DarkTheme;fadeTo=prefs.DarkTheme?1:0;
            UpdateThemeAction();prefs.Save();themeWatch.Restart();themeTimer.Start();
        }
        void AdvanceTheme(){
            double t=Math.Min(1,themeWatch.Elapsed.TotalMilliseconds/230.0);
            double ease=t*t*(3-2*t);
            if(t>=1){themeTimer.Stop();themeWatch.Stop();}
            ApplyPalette(new Theme(fadeFrom+(fadeTo-fadeFrom)*ease),t>=1);
        }
        void PaintStrip(ToolStrip strip,Color background){
            var p=Theme.Current;strip.BackColor=background;strip.ForeColor=p.Ink;
            if(strip.Renderer!=paletteRenderer)strip.Renderer=paletteRenderer;
            foreach(ToolStripItem item in strip.Items){var list=item as ToolStripDropDownItem;
                if(list!=null&&list.HasDropDownItems)PaintStrip(list.DropDown,p.Soft);}
            strip.Invalidate();
        }
        void ApplyPalette(Theme p,bool settled){
            Theme.Current=p;BackColor=editors.BackColor=output.BackColor=p.Paper;
            horizontal.BackColor=vertical.BackColor=p.Line;vertical.Panel2.BackColor=p.Soft;
            PaintStrip(menu,p.Soft);PaintStrip(tools,p.Paper);PaintStrip(tabs,p.Soft);PaintStrip(outputTabs,p.Soft);
            if(tools.OverflowButton.HasDropDownItems){var overflow=tools.OverflowButton.DropDown;overflow.Renderer=paletteRenderer;overflow.BackColor=p.Soft;overflow.ForeColor=p.Ink;}
            PaintStrip(status.Owner,p.Soft);restoreOutput.LinkColor=p.Accent;restoreOutput.ActiveLinkColor=p.Ink;
            log.BackColor=transcript.BackColor=issues.BackColor=locals.BackColor=p.Paper;
            log.ForeColor=transcript.ForeColor=locals.ForeColor=p.Ink;
            if(issues.ContextMenuStrip!=null)PaintStrip(issues.ContextMenuStrip,p.Soft);
            stateLabel.ForeColor=p.Accent;frameLabel.ForeColor=p.Muted;stateLabel.Parent.BackColor=p.Soft;
            foreach(var strip in vertical.Panel2.Controls.OfType<ToolStrip>())PaintStrip(strip,p.Soft);
            if(current!=null)current.ApplyTheme(p);
            if(finder!=null&&!finder.IsDisposed)Theme.Dialog(finder);
            if(settled){
                Theme.CaptionTheme(this,prefs.DarkTheme);if(finder!=null&&!finder.IsDisposed)Theme.CaptionTheme(finder,prefs.DarkTheme);
                RecolorIssues();locals.Invalidate();
            }
        }
        void RecolorIssues(){
            if(current==null||current.Problems.Count==0)return;
            int start=issues.SelectionStart,length=issues.SelectionLength;
            var scroll=new Point();if(issues.IsHandleCreated)Theme.RichScroll(issues.Handle,0x4dd,IntPtr.Zero,ref scroll);
            shownIssues=null;RenderIssues(current.Problems);issues.Select(Math.Min(start,issues.TextLength),Math.Min(length,issues.TextLength-Math.Min(start,issues.TextLength)));
            if(issues.IsHandleCreated)Theme.RichScroll(issues.Handle,0x4de,IntPtr.Zero,ref scroll);
        }
    }
}
