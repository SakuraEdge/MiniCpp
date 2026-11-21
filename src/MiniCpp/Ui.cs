using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Xml.Linq;

namespace MiniCpp {
    internal static class Ui {
        public static Color Ink{get{return Theme.Current.Ink;}}public static Color Muted{get{return Theme.Current.Muted;}}public static Color Soft{get{return Theme.Current.Soft;}}public static Color Line{get{return Theme.Current.Line;}}public static Color Accent{get{return Theme.Current.Accent;}}
        public static readonly Font TextFont=new Font("Microsoft YaHei UI",10F),Mono=new Font("Consolas",11F);
        public static readonly string Assets=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"assets"); static readonly Dictionary<string,Image> images=new Dictionary<string,Image>();
        public static Image Art(string name){Image img;if(!images.TryGetValue(name,out img)){using(var src=Image.FromFile(Path.Combine(Assets,name+".png")))img=new Bitmap(src);images.Add(name,img);}return img;}
        public static ToolStrip Strip(Color bg,bool overflow){return new ToolStrip{GripStyle=ToolStripGripStyle.Hidden,CanOverflow=overflow,BackColor=bg,ForeColor=Ink,Renderer=new FlatStrip(),Font=TextFont,ImageScalingSize=new Size(18,18),Padding=new Padding(7,2,6,2),AutoSize=false,Height=35,TabStop=true};}
        public static ToolStripButton Button(string art,string hint,Action action){var b=new ToolStripButton{Name=art,Image=Art(art),ToolTipText=hint,AccessibleName=hint,DisplayStyle=ToolStripItemDisplayStyle.Image,AutoSize=false,Width=38,Height=36,Margin=Padding.Empty,ImageTransparentColor=Color.Transparent};b.Click+=(s,e)=>action();return b;}
        public static TextBox Viewer(){return new TextBox{Multiline=true,ReadOnly=true,BackColor=Color.White,ForeColor=Ink,BorderStyle=BorderStyle.None,Font=Mono,Dock=DockStyle.Fill,ScrollBars=ScrollBars.Both,WordWrap=false};}
        public static Label Caption(string text){return new Label{Text=text,AutoSize=true,ForeColor=Muted,Font=TextFont,Margin=new Padding(0,3,6,2)};}
        public static string Shortcut(Keys key){return new KeysConverter().ConvertToString(key);}
    }
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
    internal sealed class Preferences {
        public string Bin="",Standard="14";public bool O2,Sampling=true,OutputCollapsed,LargeTools,DarkTheme;public int OutputPercent=26;
        public readonly int[] FontSizes=(int[])FontSettings.Defaults.Clone();
        static string FileName{get{return Path.Combine(Native.DataDir,"preferences.xml");}}
        public static Preferences Load(){var p=new Preferences();try{if(File.Exists(FileName)){var x=XElement.Load(FileName);p.DarkTheme=(bool?)x.Attribute("darkTheme")??false;p.Bin=(string)x.Attribute("bin")??"";var std=(string)x.Attribute("standard");if(Compiler.Standards.Contains(std))p.Standard=std;p.O2=(bool?)x.Attribute("o2")??false;p.Sampling=(bool?)x.Attribute("sampling")??true;p.OutputCollapsed=(bool?)x.Attribute("outputCollapsed")??false;p.OutputPercent=Math.Max(1,Math.Min(99,(int?)x.Attribute("outputPercent")??26));p.LargeTools=(bool?)x.Attribute("largeTools")??false;for(int i=0;i<p.FontSizes.Length;i++){int v;if(int.TryParse((string)x.Attribute(FontSettings.Keys[i]),out v))p.FontSizes[i]=Math.Max(8,Math.Min(24,v));}}}catch{}return p;}
        public void Save(){try{Directory.CreateDirectory(Native.DataDir);var x=new XElement("MiniCpp",new XAttribute("darkTheme",DarkTheme),new XAttribute("bin",Bin),new XAttribute("standard",Standard),new XAttribute("o2",O2),new XAttribute("sampling",Sampling),new XAttribute("largeTools",LargeTools),new XAttribute("outputPercent",OutputPercent),new XAttribute("outputCollapsed",OutputCollapsed));for(int i=0;i<FontSizes.Length;i++)x.SetAttributeValue(FontSettings.Keys[i],FontSizes[i]);x.Save(FileName);}catch(IOException){}catch(UnauthorizedAccessException){}}
    }
    internal sealed class FindDialog:Form {
        readonly Func<SourceDocument> source;readonly TextBox term=new TextBox(),replacement=new TextBox();readonly CheckBox match=new CheckBox{Text="区分大小写",AutoSize=true};readonly Label result=Ui.Caption("");
        public FindDialog(Func<SourceDocument> source,bool replace,string selection){this.source=source;Text=replace?"查找和替换":"查找";Font=Ui.TextFont;AutoScaleMode=AutoScaleMode.Font;FormBorderStyle=FormBorderStyle.FixedToolWindow;StartPosition=FormStartPosition.CenterParent;ShowInTaskbar=false;ClientSize=new Size(380,replace?175:140);
            var table=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(12),ColumnCount=2,AutoSize=true};table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));int row=0;
            table.Controls.Add(Ui.Caption("查找"),0,row);term.Dock=DockStyle.Fill;table.Controls.Add(term,1,row++);if(replace){table.Controls.Add(Ui.Caption("替换为"),0,row);replacement.Dock=DockStyle.Fill;table.Controls.Add(replacement,1,row++);}table.Controls.Add(match,1,row++);var actions=new FlowLayoutPanel{Dock=DockStyle.Fill,AutoSize=true};var find=Add(actions,"下一个",()=>Search(false));if(replace){Add(actions,"替换",()=>Search(true));Add(actions,"全部替换",ReplaceAll);}table.Controls.Add(actions,1,row++);table.Controls.Add(result,1,row);Controls.Add(table);AcceptButton=find;term.Text=selection.Contains("\n")?"":selection;Shown+=(s,e)=>{term.Focus();term.SelectAll();};KeyPreview=true;KeyDown+=(s,e)=>{if(e.KeyCode==Keys.Escape)Close();};}
        Button Add(Control p,string name,Action fn){var b=new Button{Text=name,AutoSize=true,Margin=new Padding(0,0,4,0)};b.Click+=(s,e)=>fn();p.Controls.Add(b);return b;}
        StringComparison Compare {get{return match.Checked?StringComparison.Ordinal:StringComparison.OrdinalIgnoreCase;}}
        void Search(bool replace){var d=source();if(d==null||term.Text.Length==0)return;var e=d.Edit;if(replace&&!e.ReadOnly&&string.Equals(e.SelectedText,term.Text,Compare))e.ReplaceSelection(replacement.Text);string text=e.Text;int i=text.IndexOf(term.Text,Math.Min(e.SelectionEnd,text.Length),Compare);if(i<0)i=text.IndexOf(term.Text,Compare);if(i<0){result.Text="没有匹配项";return;}e.SetSel(i,i+term.Text.Length);e.ScrollCaret();result.Text="第 "+(e.LineFromPosition(i)+1)+" 行";}
        void ReplaceAll(){var d=source();if(d==null||d.Edit.ReadOnly||term.Text.Length==0)return;var e=d.Edit;string text=e.Text;var positions=new List<int>();for(int i=0;(i=text.IndexOf(term.Text,i,Compare))>=0;i+=term.Text.Length)positions.Add(i);e.BeginUndoAction();try{for(int j=positions.Count-1;j>=0;j--){e.TargetStart=positions[j];e.TargetEnd=positions[j]+term.Text.Length;e.ReplaceTarget(replacement.Text);}}finally{e.EndUndoAction();}result.Text="已替换 "+positions.Count+" 处";}
    }
}
