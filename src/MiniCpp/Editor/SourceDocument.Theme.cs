using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using ScintillaNET;

namespace MiniCpp {
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
