using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace MiniCpp {
    // Keep indices in UTF-16 (the unit used by ScintillaNET). Mask comments before
    // looking for the end of the preceding statement; never lint literal contents.
    internal sealed class DiagnosticSource {
        readonly string[] code;
        public readonly List<Diagnostic> Punctuation=new List<Diagnostic>();
        public DiagnosticSource(string[] lines){code=new string[lines.Length];bool block=false,continuedComment=false;char quote='\0';string rawEnd=null;
            for(int n=0;n<lines.Length;n++){string line=lines[n];char[] masked=line.ToCharArray();bool splice=line.EndsWith("\\",StringComparison.Ordinal);
                if(continuedComment){code[n]="";continuedComment=splice;continue;}
                for(int i=0;i<line.Length;i++){char c=line[i];char next=i+1<line.Length?line[i+1]:'\0';
                    if(rawEnd!=null){int end=line.IndexOf(rawEnd,i,StringComparison.Ordinal);if(end<0)break;i=end+rawEnd.Length-1;rawEnd=null;continue;}
                    if(block){masked[i]=' ';if(c=='*'&&next=='/'){masked[++i]=' ';block=false;}continue;}
                    if(quote!='\0'){if(c=='\\')i++;else if(c==quote)quote='\0';continue;}
                    if(c=='/'&&next=='/'){for(int j=i;j<masked.Length;j++)masked[j]=' ';continuedComment=splice;break;}
                    if(c=='/'&&next=='*'){masked[i]=masked[++i]=' ';block=true;continue;}
                    if(c=='R'&&next=='"'){int end=line.IndexOf('(',i+2);if(end>=i+2&&end-i-2<=16){var delimiter=line.Substring(i+2,end-i-2);if(!Regex.IsMatch(delimiter,@"[\s()\\]")){rawEnd=")"+delimiter+"\"";i=end;continue;}}}
                    if(c=='"'||c=='\''){quote=c;continue;}
                    string replacement=Replacement(c);if(replacement==null)continue;
                    string label=c=='\u3000'?"全角空格":"中文标点「"+c+"」";
                    Punctuation.Add(new Diagnostic{Line=n+1,HintLine=n+1,Column=i+1,HintColumn=i+1,Severity="error",Message="U+"+((int)c).ToString("X4"),Suggestion="这里使用了"+label+"，请换成"+(replacement==" "?"半角空格。":"英文半角字符「"+replacement+"」。")});
                    // Quotes typed using a Chinese IME: only flag delimiters, not the message between them.
                    if(c=='“'||c=='‘'){char close=c=='“'?'”':'’';int j=line.IndexOf(close,i+1);if(j>=0)i=j-1;}
                }if(!splice)quote='\0';code[n]=new string(masked);
            }
        }
        static string Replacement(char c){if(c=='\u3000')return " ";if(c=='。')return ".";if(c=='、')return ",";if(c=='“'||c=='”')return "\"";if(c=='‘'||c=='’')return "'";if(c=='【'||c=='】')return c=='【'?"[":"]";if(c=='《'||c=='》')return c=='《'?"<":">";if(c>=0xff01&&c<=0xff5e){char half=(char)(c-0xfee0);if(!char.IsLetterOrDigit(half)&&half!='_')return half.ToString();}return null;}
        // GCC with LC_ALL=C renders source bytes as individual columns, with tab stops at 8.
        public static int EditorColumn(string line,int reported){int col=1;for(int i=0;i<line.Length;){int chars=char.IsSurrogatePair(line,i)?2:1;char c=line[i];int next=c=='\t'?col+(8-(col-1)%8):col+(c<128?1:c<2048?2:chars==2?4:3);if(reported<next)return i+1;col=next;i+=chars;}return line.Length+1;}
        public bool TrySemicolon(Diagnostic d){int n=d.Line-1;if(n<0||n>=code.Length)return false;string left=d.Column>0?code[n].Substring(0,Math.Min(code[n].Length,d.Column-1)).TrimEnd():"";
            if(left.Length==0){while(--n>=0){left=code[n].TrimEnd();if(left.Length>0)break;}}
            if(n<0||left.Length==0||left.TrimStart().StartsWith("#")||";{:,".IndexOf(left[left.Length-1])>=0)return false;
            d.HintLine=n+1;d.HintColumn=left.Length+1;return true;}
        public static string UnescapeName(string text){return Regex.Replace(text,@"\\(?:U([0-9a-fA-F]{8})|u([0-9a-fA-F]{4}))",m=>{uint u=Convert.ToUInt32(m.Groups[m.Groups[1].Success?1:2].Value,16);return u<=0x10ffff&&(u<0xd800||u>0xdfff)?char.ConvertFromUtf32((int)u):m.Value;});}
    }
}
