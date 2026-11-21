using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Xml.Linq;

namespace MiniCpp {
    internal sealed class FlatStrip:ToolStripSystemRenderer {
        protected override void OnRenderDropDownButtonBackground(ToolStripItemRenderEventArgs e){if(e.Item.Name!="build-standard"){base.OnRenderDropDownButtonBackground(e);return;}int pad=Math.Max(1,e.ToolStrip.DeviceDpi/96);var r=new Rectangle(pad,2*pad,e.Item.Width-2*pad-1,e.Item.Height-4*pad-1);var old=e.Graphics.SmoothingMode;e.Graphics.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;using(var path=OptimizationSwitch.Round(r,6*pad))using(var b=new SolidBrush(e.Item.Selected?Theme.Current.Hover:Ui.Soft))using(var border=new Pen(Ui.Line)){e.Graphics.FillPath(b,path);e.Graphics.DrawPath(border,path);}e.Graphics.SmoothingMode=old;}
        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e){e.Graphics.Clear(e.ToolStrip.BackColor);}
        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e){using(var p=new Pen(Ui.Line))e.Graphics.DrawLine(p,0,e.ToolStrip.Height-1,e.ToolStrip.Width,e.ToolStrip.Height-1);}
        protected override void OnRenderButtonBackground(ToolStripItemRenderEventArgs e){var b=e.Item as ToolStripButton;bool tab=b!=null&&b.Tag is SourceDocument;var t=Theme.Current;Color bg=b!=null&&b.Checked?(tab?t.Paper:t.Active):e.Item.Selected?t.Hover:e.ToolStrip.BackColor;using(var brush=new SolidBrush(bg))e.Graphics.FillRectangle(brush,new Rectangle(Point.Empty,e.Item.Size));if(tab&&b.Checked)using(var p=new Pen(Ui.Accent,2))e.Graphics.DrawLine(p,0,1,b.Width,1);}
        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e){if(e.Item.Selected||e.Item.Pressed)using(var b=new SolidBrush(Theme.Current.Hover))e.Graphics.FillRectangle(b,new Rectangle(Point.Empty,e.Item.Size));}
        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e){e.TextColor=e.Item.Enabled?Ui.Ink:Ui.Muted;base.OnRenderItemText(e);}
        protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e){e.ArrowColor=Ui.Ink;base.OnRenderArrow(e);}
        protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e){var r=e.ImageRectangle;using(var pen=new Pen(Ui.Accent,2))e.Graphics.DrawLines(pen,new[]{new Point(r.Left+r.Width/5,r.Top+r.Height/2),new Point(r.Left+r.Width*2/5,r.Top+r.Height*3/4),new Point(r.Left+r.Width*4/5,r.Top+r.Height/4)});}
        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e){using(var pen=new Pen(Ui.Line)){if(e.Vertical)e.Graphics.DrawLine(pen,e.Item.Width/2,4,e.Item.Width/2,e.Item.Height-4);else e.Graphics.DrawLine(pen,4,e.Item.Height/2,e.Item.Width-4,e.Item.Height/2);}}
        protected override void OnRenderImageMargin(ToolStripRenderEventArgs e){using(var b=new SolidBrush(Theme.Current.Soft))e.Graphics.FillRectangle(b,e.AffectedBounds);}
        protected override void OnRenderItemImage(ToolStripItemImageRenderEventArgs e){float t=(float)Theme.Current.Amount;if(t<=0||e.Image==null||e.Item.Tag is SourceDocument){base.OnRenderItemImage(e);return;}float k=1-1.82f*t;var m=new System.Drawing.Imaging.ColorMatrix();m.Matrix00=m.Matrix11=m.Matrix22=k;m.Matrix40=m.Matrix41=m.Matrix42=.99f*t;using(var a=new System.Drawing.Imaging.ImageAttributes()){a.SetColorMatrix(m);e.Graphics.DrawImage(e.Image,e.ImageRectangle,0,0,e.Image.Width,e.Image.Height,GraphicsUnit.Pixel,a);}}
    }
}
