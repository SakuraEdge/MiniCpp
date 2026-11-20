using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Xml.Linq;

namespace MiniCpp {
    internal static class Ui {
        public static readonly Color Ink=Color.FromArgb(37,37,37),Muted=Color.FromArgb(98,98,98),Soft=Color.FromArgb(247,247,247),Line=Color.FromArgb(222,222,222),Accent=Color.FromArgb(70,108,145);
        public static readonly Font TextFont=new Font("Microsoft YaHei UI",9F),Mono=new Font("Consolas",10F);
        public static readonly string Assets=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"assets"); static readonly Dictionary<string,Image> images=new Dictionary<string,Image>();
        public static Image Art(string name){Image img;if(!images.TryGetValue(name,out img)){using(var src=Image.FromFile(Path.Combine(Assets,name+".png")))img=new Bitmap(src);images.Add(name,img);}return img;}
        public static ToolStrip Strip(Color bg,bool overflow){return new ToolStrip{GripStyle=ToolStripGripStyle.Hidden,CanOverflow=overflow,BackColor=bg,ForeColor=Ink,Renderer=new FlatStrip(),Font=TextFont,ImageScalingSize=new Size(18,18),Padding=new Padding(7,2,6,2),AutoSize=false,Height=35,TabStop=true};}
        public static ToolStripButton Button(string art,string hint,Action action){var b=new ToolStripButton{Image=Art(art),ToolTipText=hint,AccessibleName=hint,DisplayStyle=ToolStripItemDisplayStyle.Image,AutoSize=false,Width=29,Height=26,Margin=new Padding(0),ImageTransparentColor=Color.Transparent};b.Click+=(s,e)=>action();return b;}
        public static TextBox Viewer(){return new TextBox{Multiline=true,ReadOnly=true,BackColor=Color.White,ForeColor=Ink,BorderStyle=BorderStyle.None,Font=Mono,Dock=DockStyle.Fill,ScrollBars=ScrollBars.Both,WordWrap=false};}
        public static Label Caption(string text){return new Label{Text=text,AutoSize=true,ForeColor=Muted,Font=TextFont,Margin=new Padding(0,3,6,2)};}
        public static string Shortcut(Keys key){return new KeysConverter().ConvertToString(key);}
    }
    internal sealed class FlatStrip:ToolStripSystemRenderer {
        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e){e.Graphics.Clear(e.ToolStrip.BackColor);}
        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e){using(var p=new Pen(Ui.Line))e.Graphics.DrawLine(p,0,e.ToolStrip.Height-1,e.ToolStrip.Width,e.ToolStrip.Height-1);}
        protected override void OnRenderButtonBackground(ToolStripItemRenderEventArgs e){var b=e.Item as ToolStripButton;bool tab=b!=null&&b.Tag is SourceDocument;Color bg=b!=null&&b.Checked?(tab?Color.White:Color.FromArgb(233,239,244)):e.Item.Selected?Color.FromArgb(237,237,237):e.ToolStrip.BackColor;using(var brush=new SolidBrush(bg))e.Graphics.FillRectangle(brush,new Rectangle(Point.Empty,e.Item.Size));if(tab&&b.Checked)using(var p=new Pen(Ui.Accent,2))e.Graphics.DrawLine(p,0,1,b.Width,1);}
    }
    internal sealed class Preferences {
        public string Bin="",Standard="14";public bool O2,Sampling=true;
        static string FileName{get{return Path.Combine(Native.DataDir,"preferences.xml");}}
        public static Preferences Load(){var p=new Preferences();try{if(File.Exists(FileName)){var x=XElement.Load(FileName);p.Bin=(string)x.Attribute("bin")??"";var std=(string)x.Attribute("standard");if(Compiler.Standards.Contains(std))p.Standard=std;p.O2=(bool?)x.Attribute("o2")??false;p.Sampling=(bool?)x.Attribute("sampling")??true;}}catch{}return p;}
        public void Save(){try{Directory.CreateDirectory(Native.DataDir);new XElement("MiniCpp",new XAttribute("bin",Bin),new XAttribute("standard",Standard),new XAttribute("o2",O2),new XAttribute("sampling",Sampling)).Save(FileName);}catch(IOException){}catch(UnauthorizedAccessException){}}
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
