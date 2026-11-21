using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using ScintillaNET;

namespace MiniCpp {
    // The same roles drive the editor, chrome and diagnostics at every animation frame.
    internal sealed class Theme {
        internal static Theme Current=new Theme(0);
        public readonly double Amount;
        public readonly Color Paper,Soft,Ink,Muted,Line,Hover,Active,Accent,Selection,Keyword,Comment,Directive,Operator,Literal,Number,Error,ErrorLine,Breakpoint,Execution,ExecutionLine;
        public Theme(double amount){Amount=amount;
            Paper=C(0xffffff,0x202224);Soft=C(0xf7f7f7,0x292b2d);Ink=C(0x252525,0xe4e5e5);Muted=C(0x626262,0xafb3b5);Line=C(0xdedede,0x45484b);
            Hover=C(0xedeeee,0x373a3d);Active=C(0xe9eff4,0x384652);Accent=C(0x466c91,0x9bc0de);Selection=C(0xd2e1ee,0x40586c);
            Keyword=C(0x1e1e1e,0xf3f3f2);Comment=C(0x666e69,0x9ba69e);Directive=C(0x12702e,0x88c99a);Operator=C(0xc02d30,0xf89699);
            Literal=C(0x375b6f,0xa2c8dc);Number=C(0x825622,0xe3b988);Error=C(0x913634,0xffa5a3);ErrorLine=C(0xffe5e6,0x512f34);
            Breakpoint=C(0xb0393c,0xa8484e);Execution=C(0x9d6416,0xe7b866);ExecutionLine=C(0xfff4d9,0x50432f);
        }
        Color C(int a,int b){return Color.FromArgb(Channel(a>>16,b>>16),Channel(a>>8,b>>8),Channel(a,b));}
        int Channel(int a,int b){a&=255;b&=255;return (int)Math.Round(a+(b-a)*Amount);}
        [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd,int attribute,ref int value,int size);
        [DllImport("user32.dll",EntryPoint="SendMessageW")] internal static extern IntPtr RichScroll(IntPtr hwnd,int msg,IntPtr unused,ref Point point);
        internal static void CaptionTheme(Form f,bool dark){if(!f.IsHandleCreated)return;int on=dark?1:0;try{if(DwmSetWindowAttribute(f.Handle,20,ref on,4)!=0)DwmSetWindowAttribute(f.Handle,19,ref on,4);}catch(DllNotFoundException){}catch(EntryPointNotFoundException){}}
        internal static void Dialog(Control c){var p=Current;var btn=c as Button;c.BackColor=(c is TextBoxBase||c is NumericUpDown)?p.Paper:p.Soft;c.ForeColor=p.Ink;
            if(btn!=null){btn.FlatStyle=FlatStyle.Flat;btn.FlatAppearance.BorderColor=p.Line;btn.FlatAppearance.MouseOverBackColor=p.Hover;}
            foreach(Control child in c.Controls)Dialog(child);
        }
    }

    internal sealed partial class SourceDocument {
        Theme lastPalette;
        internal void ApplyTheme(Theme p){if(lastPalette==p)return;lastPalette=p;var e=Edit;
            // Only change color attributes: style IDs, text, selection, undo history and markers stay intact.
            for(int i=0;i<=Style.Cpp.EscapeSequence;i++){e.Styles[i].BackColor=p.Paper;e.Styles[i].ForeColor=p.Ink;}
            e.Styles[Style.Default].BackColor=p.Paper;e.Styles[Style.Default].ForeColor=p.Ink;
            foreach(int i in new[]{Style.Cpp.Comment,Style.Cpp.CommentLine,Style.Cpp.CommentDoc,Style.Cpp.CommentLineDoc,Style.Cpp.CommentDocKeyword,Style.Cpp.PreprocessorComment,Style.Cpp.PreprocessorCommentDoc})e.Styles[i].ForeColor=p.Comment;
            foreach(int i in new[]{Style.Cpp.String,Style.Cpp.Character,Style.Cpp.StringEol,Style.Cpp.StringRaw,Style.Cpp.EscapeSequence})e.Styles[i].ForeColor=p.Literal;
            e.Styles[Style.Cpp.Word].ForeColor=p.Keyword;e.Styles[Style.Cpp.Preprocessor].ForeColor=p.Directive;e.Styles[Style.Cpp.Operator].ForeColor=p.Operator;e.Styles[Style.Cpp.Number].ForeColor=p.Number;
            e.Styles[Style.LineNumber].BackColor=p.Soft;e.Styles[Style.LineNumber].ForeColor=p.Muted;e.Styles[Style.IndentGuide].BackColor=p.Paper;e.Styles[Style.IndentGuide].ForeColor=p.Line;
            e.Styles[Style.CallTip].BackColor=p.Soft;e.Styles[Style.CallTip].ForeColor=p.Ink;e.CallTipSetForeHlt(p.Accent);
            e.CaretForeColor=e.AdditionalCaretForeColor=p.Ink;e.SetSelectionBackColor(true,p.Selection);e.SetSelectionForeColor(true,p.Ink);
            e.Markers[ErrorMarker].SetBackColor(p.ErrorLine);e.Markers[2].SetBackColor(p.ExecutionLine);
            Surface.BackColor=p.Paper;Gutter.BackColor=p.Soft;Gutter.Invalidate();
        }
    }
}
