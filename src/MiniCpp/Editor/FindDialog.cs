using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Xml.Linq;

namespace MiniCpp {
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
