using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace MiniCpp {
    internal static class FontSettings {
        internal static readonly string[] Keys={"uiPt","sourcePt","issuePt","logPt","outputPt","debugPt"};
        internal static readonly int[] Defaults={10,11,13,12,12,11};
        static readonly string[] Labels={"菜单、标签和状态栏","代码和行号","问题与中文建议","编译日志","运行输出","调试变量"};
        static readonly Dictionary<string,Font> cache=new Dictionary<string,Font>();
        internal static Font Get(int points,bool mono){string family=mono?"Consolas":"Microsoft YaHei UI",key=family+points;Font f;if(!cache.TryGetValue(key,out f)){f=new Font(family,points,FontStyle.Regular,GraphicsUnit.Point);cache.Add(key,f);}return f;}
        internal static bool Edit(IWin32Window owner,int[] values){using(var w=new Form{Text="字体大小",Font=Ui.TextFont,AutoScaleDimensions=new SizeF(96,96),AutoScaleMode=AutoScaleMode.Dpi,StartPosition=FormStartPosition.CenterParent,FormBorderStyle=FormBorderStyle.FixedDialog,MaximizeBox=false,MinimizeBox=false,ShowInTaskbar=false,AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink}){
            var body=new TableLayoutPanel{AutoSize=true,ColumnCount=3,Padding=new Padding(16)};var nums=new NumericUpDown[values.Length];
            for(int i=0;i<nums.Length;i++){nums[i]=new NumericUpDown{Minimum=8,Maximum=24,Value=values[i],Width=60};body.Controls.Add(new Label{Text=Labels[i],Anchor=AnchorStyles.Left,AutoSize=true,Margin=new Padding(0,5,26,5)},0,i);body.Controls.Add(nums[i],1,i);body.Controls.Add(new Label{Text="pt",AutoSize=true,Anchor=AnchorStyles.Left,ForeColor=Ui.Muted},2,i);}
            var row=new FlowLayoutPanel{AutoSize=true,Margin=new Padding(0,12,0,0)};var reset=new Button{Text="恢复默认",AutoSize=true};var ok=new Button{Text="确定",AutoSize=true,DialogResult=DialogResult.OK};var cancel=new Button{Text="取消",AutoSize=true,DialogResult=DialogResult.Cancel};reset.Click+=(s,e)=>{for(int i=0;i<nums.Length;i++)nums[i].Value=Defaults[i];};row.Controls.AddRange(new Control[]{reset,ok,cancel});body.Controls.Add(row,0,nums.Length);body.SetColumnSpan(row,3);w.Controls.Add(body);w.AcceptButton=ok;w.CancelButton=cancel;if(w.ShowDialog(owner)!=DialogResult.OK)return false;for(int i=0;i<nums.Length;i++)values[i]=(int)nums[i].Value;return true;
        }}
    }
    internal sealed class OptimizationSwitch:ToolStripButton {
        public OptimizationSwitch(){Name="build-optimize";Text="O2";AccessibleName="O2 编译优化";AccessibleRole=AccessibleRole.CheckButton;DisplayStyle=ToolStripItemDisplayStyle.Text;AutoSize=false;CheckOnClick=true;}
        protected override void OnPaint(PaintEventArgs e){var g=e.Graphics;float unit=Owner==null?1f:Owner.DeviceDpi/96f;int w=(int)Math.Round(24*unit),h=(int)Math.Round(14*unit),gap=(int)Math.Round(5*unit);var r=new Rectangle(Width-w-gap,(Height-h)/2,w,h);
            if(Selected||Pressed)using(var b=new SolidBrush(Ui.Soft))g.FillRectangle(b,new Rectangle(Point.Empty,Size));
            TextRenderer.DrawText(g,Text,Font,new Rectangle(0,0,r.Left-gap,Height),Ui.Ink,TextFormatFlags.Right|TextFormatFlags.VerticalCenter|TextFormatFlags.NoPadding);
            var old=g.SmoothingMode;g.SmoothingMode=SmoothingMode.AntiAlias;using(var path=Round(r,h))using(var b=new SolidBrush(Checked?Ui.Accent:Color.FromArgb(158,163,167)))g.FillPath(b,path);
            int p=Math.Max(2,(int)Math.Round(2*unit)),d=h-2*p;g.FillEllipse(Brushes.White,Checked?r.Right-p-d:r.Left+p,r.Top+p,d,d);g.SmoothingMode=old;
        }
        internal static GraphicsPath Round(Rectangle r,int dia){var p=new GraphicsPath();dia=Math.Max(2,Math.Min(dia,Math.Min(r.Width,r.Height)));p.AddArc(r.X,r.Y,dia,dia,180,90);p.AddArc(r.Right-dia,r.Y,dia,dia,270,90);p.AddArc(r.Right-dia,r.Bottom-dia,dia,dia,0,90);p.AddArc(r.X,r.Bottom-dia,dia,dia,90,90);p.CloseFigure();return p;}
    }
}
