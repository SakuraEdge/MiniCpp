using System;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using ScintillaNET;

namespace MiniCpp {
    internal sealed class PairedEditor : Scintilla {
        const int AutoCloseIndicator=20, WmChar=0x102, WmUniChar=0x109, WmKeyDown=0x100, SciSelectionIsRectangle=2372;
        const string Open="([{\"'<", Close=")]}\"'>";
        static readonly Regex IncludePrefix=new Regex(@"^\s*#\s*include(?:_next)?\s*$",RegexOptions.CultureInvariant);
        static readonly Regex RawPrefix=new Regex(@"(?:^|\W)(?:u8|[uUL])?R$",RegexOptions.CultureInvariant);
        char suppress;

        public PairedEditor(){Technology=Technology.DirectWrite;Indicators[AutoCloseIndicator].Style=IndicatorStyle.Hidden;}
        protected override void WndProc(ref Message m){
            // A text service can commit a scalar without a key-down. Scintilla 3.7.2's
            // WM_UNICHAR path can discard it after a consumed key (e.g. Left Arrow).
            // Its wide WM_CHAR path already joins surrogate pairs and honors native undo/IME.
            if(m.Msg==WmUniChar){long scalar=m.WParam.ToInt64();if(scalar==0xffff){m.Result=(IntPtr)1;return;}
                if(scalar>=0x20&&scalar<=0x10ffff&&(scalar<0xd800||scalar>0xdfff)){
                    foreach(char unit in char.ConvertFromUtf32((int)scalar)){var part=Message.Create(m.HWnd,WmChar,(IntPtr)unit,m.LParam);DefWndProc(ref part);}
                }m.Result=IntPtr.Zero;return;
            }
            if(m.Msg==WmChar&&char.IsSurrogate((char)m.WParam.ToInt32())){DefWndProc(ref m);return;}
            if(m.Msg==WmKeyDown)suppress='\0';
            if(m.Msg==WmChar&&suppress!='\0'){char old=suppress;suppress='\0';if((char)m.WParam.ToInt32()==old)return;}
            if((m.Msg==WmKeyDown||m.Msg==WmChar)&&m.WParam.ToInt32()=='\r'&&ModifierKeys==Keys.None&&!ReadOnly&&ExpandCommand()){
                if(m.Msg==WmKeyDown)suppress='\r';return;
            }
            if((m.Msg==WmChar||m.Msg==WmKeyDown)&&!ReadOnly&&!Overtype&&Selections.Count==1&&DirectMessage(SciSelectionIsRectangle,IntPtr.Zero,IntPtr.Zero)==IntPtr.Zero){
                char key=(char)m.WParam.ToInt32();
                if(m.Msg==WmChar&&(Open.IndexOf(key)>=0||Close.IndexOf(key)>=0)&&TypeDelimiter(key))return;
                if(m.Msg==WmKeyDown&&ModifierKeys==Keys.None&&(key=='\b'||key=='\r')&&EditEmptyPair(key)){suppress=key;return;}
            }
            base.WndProc(ref m);
        }

        bool ExpandCommand(){
            if(SelectionStart!=SelectionEnd||Selections.Count!=1||DirectMessage(SciSelectionIsRectangle,IntPtr.Zero,IntPtr.Zero)!=IntPtr.Zero)return false;
            var line=Lines[CurrentLine];string input=line.Text.TrimEnd('\r','\n');
            int start=line.Position,end=start+input.Length;
            if(CurrentPosition!=end)return false;
            string command=input.TrimStart(' ','\t');
            string nl=EolMode==Eol.CrLf?"\r\n":EolMode==Eol.Cr?"\r":"\n";
            string code;int caretOffset;
            if(!ContestTemplates.TryExpand(command,nl,out code,out caretOffset))return false;
            Colorize(Math.Min(GetEndStyled(),start),end);
            if((GetStyleAt(end-command.Length)&63)!=Style.Cpp.Operator)return false;
            BeginUndoAction();try{TargetStart=start;TargetEnd=end;ReplaceTarget(code);SetEmptySelection(start+caretOffset);ScrollCaret();}finally{EndUndoAction();}
            return true;
        }

        bool Marked(int p){return p>=0&&p<TextLength&&Indicators[AutoCloseIndicator].ValueAt(p)!=0;}
        void Mark(int p,bool on){int old=IndicatorCurrent,value=IndicatorValue;try{IndicatorCurrent=AutoCloseIndicator;IndicatorValue=1;if(on)IndicatorFillRange(p,1);else IndicatorClearRange(p,1);}finally{IndicatorCurrent=old;IndicatorValue=value;}}
        bool Escaped(int pos){int n=0;while(pos>0&&GetCharAt(--pos)=='\\')n++;return (n&1)!=0;}
        bool TypeDelimiter(char key){
            int a=SelectionStart,b=SelectionEnd;bool quote=key=='\''||key=='\"';
            if(quote&&Escaped(a))return false;
            if(a==b&&Marked(a)&&GetCharAt(a)==key&&Close.IndexOf(key)>=0){Mark(a,false);GotoPosition(a+1);return true;}
            int pair=Open.IndexOf(key);if(pair<0)return false;
            var line=Lines[LineFromPosition(a)];
            // Only the include operand has unambiguous angle delimiters in ordinary C++ typing.
            if(key=='<'&&!IncludePrefix.IsMatch(GetTextRange(line.Position,a-line.Position)))return false;
            if(!CanPairAt(a,line))return false;
            if(quote&&key=='\"'&&RawPrefix.IsMatch(GetTextRange(line.Position,a-line.Position)))return false;
            // C++14 digit separators (1'000) are not character literals.
            if(key=='\''&&a>0&&GetStyleAt(a-1)==Style.Cpp.Number)return false;
            char closer=Close[pair];string selected=SelectedText;
            BeginUndoAction();try{ReplaceSelection(key+selected+closer);Mark(a+1+selected.Length,true);if(a==b)SetEmptySelection(a+1);else SetSel(a+1,a+1+selected.Length);}finally{EndUndoAction();}return true;
        }
        bool CanPairAt(int p,Line line){
            int end=Math.Min(TextLength,line.EndPosition+1);if(end>0)Colorize(Math.Min(GetEndStyled(),line.Position),end);
            if(p<=0)return true;int style=GetStyleAt(p-1)&63;
            switch(style){
                case Style.Cpp.CommentLine: case Style.Cpp.CommentLineDoc: return p==line.Position;
                case Style.Cpp.Comment: case Style.Cpp.CommentDoc: case Style.Cpp.PreprocessorComment: case Style.Cpp.PreprocessorCommentDoc:
                    return p>=2&&GetTextRange(p-2,2)=="*/";
                case Style.Cpp.String: case Style.Cpp.Character: case Style.Cpp.StringEol: case Style.Cpp.StringRaw: case Style.Cpp.EscapeSequence:
                    if(p==line.Position&&style==Style.Cpp.StringEol)return true;
                    int prev=GetCharAt(p-1);return (prev=='\"'||prev=='\'')&&!Escaped(p-1)&&p>=2&&(GetStyleAt(p-2)&63)==style&&(p==TextLength||(GetStyleAt(p)&63)!=style);
                case Style.Cpp.Preprocessor:
                    // Whole directives are intentionally green: the lexer does not split their strings.
                    string left=GetTextRange(line.Position,p-line.Position);char q='\0';for(int i=0;i<left.Length;i++){char c=left[i];if(q!='\0'){if(c=='\\')i++;else if(c==q)q='\0';}else if(c=='\''||c=='\"')q=c;}return q=='\0';
                default:return true;
            }
        }
        bool EditEmptyPair(char key){int p=CurrentPosition;if(p!=SelectionStart||p!=SelectionEnd||p<1||!Marked(p))return false;
            char left=(char)GetCharAt(p-1);int pair=Open.IndexOf(left);if(pair<0||GetCharAt(p)!=Close[pair])return false;
            if(key=='\r'&&left!='{')return false;
            BeginUndoAction();try{if(key=='\b'){DeleteRange(p-1,2);SetEmptySelection(p-1);}else{
                string nl=EolMode==Eol.CrLf?"\r\n":EolMode==Eol.Cr?"\r":"\n";string outer=new string(' ',Lines[LineFromPosition(p)].Indentation),inner=outer+new string(' ',Math.Max(1,IndentWidth));ReplaceSelection(nl+inner+nl+outer);SetEmptySelection(p+nl.Length+inner.Length);
            }}finally{EndUndoAction();}return true;
        }
    }
}
