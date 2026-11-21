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
}
