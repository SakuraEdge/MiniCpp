using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using ScintillaNET;

namespace MiniCpp {
    internal sealed class LineGutter : Control {
        readonly Scintilla editor;
        readonly Action<int> toggle;
        int dpi=96, points=11, digits;
        Font numberFont;

        public LineGutter(Scintilla editor,Action<int> toggle){
            this.editor=editor;this.toggle=toggle;Dock=DockStyle.Left;TabStop=false;AccessibleName="行号与断点";
            SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);
            SetStyle(ControlStyles.Selectable,false);BackColor=Color.FromArgb(249,249,249);
            editor.TextChanged+=(s,e)=>UpdateWidth(false);editor.ZoomChanged+=(s,e)=>SetMetrics(dpi,points);
            editor.Painted+=(s,e)=>{if(Visible&&!IsDisposed)Invalidate();};
        }
        int Px(int units){return Math.Max(1,(int)Math.Round(units*dpi/96.0));}
        public void SetMetrics(int newDpi,int newPoints){dpi=newDpi;points=newPoints;var old=numberFont;numberFont=new Font("Consolas",Math.Max(2,points+editor.Zoom),FontStyle.Regular,GraphicsUnit.Point);if(old!=null)old.Dispose();UpdateWidth(true);}
        void UpdateWidth(bool force){int n=editor.Lines.Count.ToString().Length;if(!force&&n==digits)return;digits=n;
            Width=editor.TextWidth(Style.LineNumber,new string('9',n))+2*Px(10);Invalidate();}
        // The native editor is the source of truth for scrolling, line height and moving markers.
        void VisibleRows(Action<int,int,int> visit){if(!editor.IsHandleCreated||editor.IsDisposed)return;int n=editor.DocLineFromVisible(editor.FirstVisibleLine);
            if(n<0||n>=editor.Lines.Count)return;int y=editor.PointYFromPosition(editor.Lines[n].Position),h=editor.Lines[n].Height;if(h<=0)return;
            for(;n<editor.Lines.Count&&y<editor.ClientSize.Height;n++){var line=editor.Lines[n];if(!line.Visible)continue;visit(n,y,h);y+=h;}}
        protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);var g=e.Graphics;g.Clear(BackColor);g.SmoothingMode=SmoothingMode.AntiAlias;
            VisibleRows((n,y,h)=>{if(y+h<=e.ClipRectangle.Top||y>=e.ClipRectangle.Bottom)return;int marks=(int)editor.Lines[n].MarkerGet();string text=(n+1).ToString();bool bp=(marks&1)!=0,exec=(marks&2)!=0;
                var p=Theme.Current;bool error=(marks&(1<<SourceDocument.ErrorMarker))!=0;
                if(error){using(var b=new SolidBrush(p.ErrorLine))g.FillRectangle(b,0,y,Width,h);using(var b=new SolidBrush(p.Error))g.FillRectangle(b,Width-Px(2),y,Px(2),h);}
                if(bp){int badgeH=Math.Max(4,h-Px(2)),badgeW=Math.Min(Width-Px(4),Math.Max(badgeH,editor.TextWidth(Style.LineNumber,text)+Px(6)));var r=new Rectangle((Width-badgeW)/2,y+(h-badgeH)/2,badgeW,badgeH);
                    using(var shape=OptimizationSwitch.Round(r,badgeH))using(var b=new SolidBrush(p.Breakpoint))g.FillPath(b,shape);}
                if(exec){int a=Px(4),m=y+h/2;using(var b=new SolidBrush(p.Execution))g.FillPolygon(b,new[]{new Point(0,m-a),new Point(a,m),new Point(0,m+a)});}
                TextRenderer.DrawText(g,text,numberFont??Font,new Rectangle(0,y,Width,h),bp?Color.White:error?p.Error:p.Muted,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.SingleLine|TextFormatFlags.NoPadding|TextFormatFlags.NoPrefix);
            });}
        protected override void OnMouseDown(MouseEventArgs e){base.OnMouseDown(e);if(e.Button!=MouseButtons.Left)return;int selected=-1;VisibleRows((n,y,h)=>{if(e.Y>=y&&e.Y<y+h)selected=n;});if(selected>=0){editor.Focus();toggle(selected);}}
        protected override void OnMouseWheel(MouseEventArgs e){if((ModifierKeys&Keys.Control)!=0){if(e.Delta>0)editor.ZoomIn();else if(e.Delta<0)editor.ZoomOut();}else{int step=SystemInformation.MouseWheelScrollLines;editor.LineScroll(-e.Delta/120*(step<0?Math.Max(1,editor.LinesOnScreen):step),0);}}
        protected override void Dispose(bool disposing){if(disposing&&numberFont!=null){numberFont.Dispose();numberFont=null;}base.Dispose(disposing);}
    }
}
