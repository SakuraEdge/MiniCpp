using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MiniCpp {
    // MI uses recursive lists of values or results and permits repeated keys (e.g. frame=...).
    internal sealed class MiNode {
        public string Key="",Text=""; public List<MiNode> Children=new List<MiNode>();
        public MiNode Get(string key){return Children.FirstOrDefault(n=>n.Key==key)??new MiNode();}
        public string Str(string key){return Get(key).Text;}
        public static MiNode Parse(string s){int i=0;var root=new MiNode();while(i<s.Length){if(s[i]==','){i++;continue;}int old=i;root.Children.Add(Read(s,ref i,0));if(i==old)break;}return root;}
        static MiNode Read(string s,ref int i,int depth){
            if(depth>48)throw new FormatException("MI 嵌套过深");var n=new MiNode();int a=i;
            while(i<s.Length&&(char.IsLetterOrDigit(s[i])||s[i]=='-'||s[i]=='_'))i++;
            if(i<s.Length&&s[i]=='='){n.Key=s.Substring(a,i-a);i++;}else i=a;
            if(i>=s.Length)return n;char c=s[i++];
            if(c=='{'||c=='['){char end=c=='{'?'}':']';while(i<s.Length&&s[i]!=end){if(s[i]==','){i++;continue;}int old=i;n.Children.Add(Read(s,ref i,depth+1));if(i==old)throw new FormatException();}if(i<s.Length)i++;}
            else if(c=='"'){var b=new StringBuilder();var oct=new List<byte>();Action flush=delegate{if(oct.Count!=0){b.Append(Encoding.UTF8.GetString(oct.ToArray()));oct.Clear();}};
                while(i<s.Length){char v=s[i++];if(v=='"')break;if(v!='\\'){flush();b.Append(v);continue;}if(i==s.Length)break;v=s[i++];if(v>='0'&&v<='7'){int k=v-'0',j=1;while(j<3&&i<s.Length&&s[i]>='0'&&s[i]<='7'){k=k*8+s[i++]-'0';j++;}oct.Add((byte)k);continue;}flush();b.Append(v=='n'?'\n':v=='r'?'\r':v=='t'?'\t':v);}
                flush();n.Text=b.ToString();}
            else{a=i-1;while(i<s.Length&&s[i]!=','&&s[i]!=']'&&s[i]!='}')i++;n.Text=s.Substring(a,i-a);}return n;
        }
    }
}
