using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace MiniCpp {
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
