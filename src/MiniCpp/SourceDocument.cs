using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using ScintillaNET;

namespace MiniCpp {
    internal static class FileCodec {
        public static string Read(string path,out Encoding enc){byte[] b=File.ReadAllBytes(path);if(b.Length>=2&&((b[0]==255&&b[1]==254)||(b[0]==254&&b[1]==255)))throw new InvalidDataException("请将 UTF-16 源文件另存为 UTF-8 后打开。");
            bool bom=b.Length>=3&&b[0]==239&&b[1]==187&&b[2]==191;try{enc=new UTF8Encoding(bom,true);return enc.GetString(b,bom?3:0,b.Length-(bom?3:0));}catch(DecoderFallbackException){enc=Encoding.GetEncoding(54936,EncoderFallback.ExceptionFallback,DecoderFallback.ExceptionFallback);return enc.GetString(b);}}
        public static void Write(string path,string content,Encoding enc){string tmp=Path.Combine(Path.GetDirectoryName(path),".minicpp-"+Guid.NewGuid().ToString("N")+".tmp");try{using(var f=new FileStream(tmp,FileMode.CreateNew,FileAccess.Write,FileShare.None))using(var w=new StreamWriter(f,enc))w.Write(content);if(File.Exists(path))File.Replace(tmp,path,null);else File.Move(tmp,path);}finally{if(File.Exists(tmp))File.Delete(tmp);}}
    }
    internal sealed class SourceDocument:IDisposable {
        public const string Template="#include <iostream>\r\n\r\nint main() {\r\n    \r\n    return 0;\r\n}\r\n";
        public readonly Scintilla Edit=new Scintilla(); public string FullPath, Standard="14", RunText="", LogText="";public int Slot;public bool O2,BuildFailed;public DateTime LastWrite;public Encoding Encoding=Native.Utf8;public InputMode IO=InputMode.Auto;
        public List<Diagnostic> Problems=new List<Diagnostic>();public string Status="就绪",BuiltSource;
        public event Action Changed,CursorChanged;public event Action<int,bool> BreakpointChanged;public Func<string,string> VariableTip;
        public string Name {get{return FullPath==null?"未命名"+Slot:Path.GetFileName(FullPath);}}
        public SourceDocument(string text,int slot){Slot=slot;var e=Edit;e.Dock=DockStyle.Fill;e.BorderStyle=BorderStyle.None;e.Lexer=Lexer.Cpp;e.UseTabs=false;e.TabWidth=4;e.IndentWidth=4;e.IndentationGuides=IndentView.LookBoth;
            e.StyleResetDefault();e.Styles[Style.Default].Font="Consolas";e.Styles[Style.Default].Size=11;e.Styles[Style.Default].ForeColor=Color.FromArgb(37,37,37);e.Styles[Style.Default].BackColor=Color.White;e.StyleClearAll();
            foreach(int i in new[]{Style.Cpp.Comment,Style.Cpp.CommentDoc,Style.Cpp.CommentLine,Style.Cpp.CommentLineDoc})e.Styles[i].ForeColor=Color.FromArgb(102,110,105);
            e.Styles[Style.Cpp.Word].ForeColor=Color.FromArgb(108,61,139);e.Styles[Style.Cpp.Word2].ForeColor=Color.FromArgb(48,92,132);e.Styles[Style.Cpp.Preprocessor].ForeColor=Color.FromArgb(48,92,132);
            e.Styles[Style.Cpp.String].ForeColor=e.Styles[Style.Cpp.Character].ForeColor=Color.FromArgb(55,91,111);e.Styles[Style.Cpp.Number].ForeColor=Color.FromArgb(130,86,34);
            e.Styles[Style.LineNumber].ForeColor=Color.DimGray;e.Styles[Style.LineNumber].BackColor=Color.FromArgb(249,249,249);
            e.SetKeywords(0,"alignas alignof and asm auto bool break case catch char char16_t char32_t class const constexpr continue decltype default delete do double else enum explicit export extern false float for friend goto if inline int long mutable namespace new noexcept nullptr operator private protected public register reinterpret_cast return short signed sizeof static static_assert static_cast struct switch template this thread_local throw true try typedef typename union unsigned using virtual void volatile wchar_t while");
            e.SetKeywords(1,"std main cin cout cerr endl vector array string pair map set multiset unordered_map queue deque priority_queue stack sort lower_bound upper_bound min max printf scanf");e.SetProperty("lexer.cpp.track.preprocessor","0");e.SetSelectionBackColor(true,Color.FromArgb(210,225,238));
            e.Margins[0].Type=MarginType.Symbol;e.Margins[0].Mask=3;e.Margins[0].Sensitive=true;
            e.Margins[1].Type=MarginType.Number;e.Margins[1].Mask=0;e.Margins[1].Sensitive=true;
            e.Margins[2].Width=0;
            Marker(0,MarkerSymbol.Circle,Color.FromArgb(177,66,65));Marker(1,MarkerSymbol.ShortArrow,Color.FromArgb(154,103,26));Marker(2,MarkerSymbol.Background,Color.FromArgb(255,244,217));
            e.MouseDwellTime=350;e.MarginClick+=delegate(object s,MarginClickEventArgs a){if((a.Margin==0||a.Margin==1)&&a.Position>=0)ToggleBreak(e.LineFromPosition(a.Position));};
            e.TextChanged+=(s,a)=>SizeGutter(false);e.ZoomChanged+=(s,a)=>SizeGutter(true);
            e.DwellStart+=delegate(object s,DwellEventArgs a){if(a.Position<0||VariableTip==null)return;int lo=e.WordStartPosition(a.Position,true),hi=e.WordEndPosition(a.Position,true);if(lo>=hi||a.Position>=hi)return;string val=VariableTip(e.GetTextRange(lo,hi-lo));if(!string.IsNullOrEmpty(val))e.CallTipShow(lo,val);};e.DwellEnd+=(s,a)=>e.CallTipCancel();
            e.EolMode=text.Contains("\r\n")?Eol.CrLf:text.Contains("\n")?Eol.Lf:Eol.CrLf;e.Text=text;e.EmptyUndoBuffer();e.SetSavePoint();if(slot>0)e.GotoPosition(e.Lines[Math.Min(3,e.Lines.Count-1)].EndPosition);
            e.SavePointLeft+=(s,a)=>{if(Changed!=null)Changed();};e.SavePointReached+=(s,a)=>{if(Changed!=null)Changed();};e.UpdateUI+=(s,a)=>{if(CursorChanged!=null)CursorChanged();};
            e.InsertCheck+=delegate(object s,InsertCheckEventArgs a){if(a.Text!="\n"&&a.Text!="\r"&&a.Text!="\r\n")return;int n=e.LineFromPosition(a.Position),lo=e.Lines[n].Position;string left=e.GetTextRange(lo,a.Position-lo);int spaces=Indent(left);if(left.TrimEnd().EndsWith("{")&&IsCode(Math.Max(lo,a.Position-1)))spaces+=4;a.Text+=new string(' ',Math.Min(2048,spaces));};
        }
        int gutterDpi=96,gutterDigits,editorPoints=-1;
        public void ApplyDisplay(int dpi,int points){gutterDpi=dpi;if(editorPoints!=points){for(int i=0;i<Edit.Styles.Count;i++)Edit.Styles[i].Size=points;editorPoints=points;}SizeGutter(true);}
        void SizeGutter(bool refresh){int digits=Math.Max(1,Edit.Lines.Count.ToString().Length);if(!refresh&&digits==gutterDigits)return;gutterDigits=digits;
            Edit.Margins[0].Width=(int)Math.Round(14*gutterDpi/96.0);Edit.Margins[1].Width=Edit.TextWidth(Style.LineNumber,new string('9',digits))+(int)Math.Round(6*gutterDpi/96.0);}
        static int Indent(string s){int k=0;foreach(char c in s){if(c==' ')k++;else if(c=='\t')k+=4-k%4;else break;}return k;}
        bool IsCode(int p){int v=Edit.GetStyleAt(p);return v!=1&&v!=2&&v!=3&&v!=6&&v!=7&&v!=12&&v!=13&&v!=15&&v!=17&&v!=18&&v!=20;}
        void Marker(int n,MarkerSymbol sym,Color c){Edit.Markers[n].Symbol=sym;Edit.Markers[n].SetBackColor(c);Edit.Markers[n].SetForeColor(c);}
        public void ToggleBreak(int line){if(line<0||line>=Edit.Lines.Count)return;bool enable=(Edit.Lines[line].MarkerGet()&1)==0;if(enable)Edit.Lines[line].MarkerAdd(0);else Edit.Lines[line].MarkerDelete(0);if(BreakpointChanged!=null)BreakpointChanged(line+1,enable);}
        public IEnumerable<int> Breakpoints(){for(int i=0;i<Edit.Lines.Count;i++)if((Edit.Lines[i].MarkerGet()&1)!=0)yield return i+1;}
        public void Location(int oneBased,int col){if(oneBased<1||oneBased>Edit.Lines.Count)return;var l=Edit.Lines[oneBased-1];l.EnsureVisible();Edit.GotoPosition(Math.Min(l.EndPosition,l.Position+Math.Max(0,col-1)));Edit.ScrollCaret();}
        public void Execution(int line){Edit.MarkerDeleteAll(1);Edit.MarkerDeleteAll(2);if(line>0&&line<=Edit.Lines.Count){Edit.Lines[line-1].MarkerAdd(1);Edit.Lines[line-1].MarkerAdd(2);Location(line,1);}}
        public void Reindent(int mode){var e=Edit;if(e.ReadOnly)return;int a=e.SelectionStart,b=e.SelectionEnd;if(mode==1&&a==b){e.ReplaceSelection(new string(' ',4-(e.GetColumn(a)%4)));return;}
            int first=e.LineFromPosition(a),last=e.LineFromPosition(b);if(b>a&&last>first&&e.Lines[last].Position==b)last--;bool selected=b>a;int caret=e.GetColumn(a);e.Colorize(0,-1);int depth=0;
            e.BeginUndoAction();try{for(int n=mode==0?0:first;n<=last;n++){var line=e.Lines[n];string text=line.Text.TrimEnd('\r','\n');int delta=0;if(mode==0){for(int i=0;i<text.Length;i++){char c=text[i];if((c=='{'||c=='}')&&IsCode(line.Position+i))delta+=c=='{'?1:-1;}}
                if(n>=first){int current=line.Indentation, closing=0;string clean=text.TrimStart();while(closing<clean.Length&&clean[closing]=='}')closing++;int wanted=mode!=0?Math.Max(0,current+4*mode):clean.Length==0||clean.StartsWith("#")?0:4*Math.Max(0,depth-closing);line.Indentation=Math.Min(wanted,2048);if(n==first)caret=Math.Max(0,caret+line.Indentation-current);}
                depth=Math.Max(0,depth+delta);}
                if(selected)e.SetSel(e.Lines[first].Position,last+1<e.Lines.Count?e.Lines[last+1].Position:e.Lines[last].EndPosition);else e.GotoPosition(Math.Min(e.Lines[first].EndPosition,e.Lines[first].Position+caret));
            }finally{e.EndUndoAction();}}
        public void Dispose(){Edit.Dispose();}
    }
}
