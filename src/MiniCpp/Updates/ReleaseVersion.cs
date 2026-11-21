using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MiniCpp {
    internal sealed class ReleaseVersion : IComparable<ReleaseVersion> {
        public string Tag,Notes; public bool FromRecentFeed; Version number; string[] suffix;
        public static ReleaseVersion Parse(string tag){var m=Regex.Match(tag??"",@"^v?(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)(?:-([0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*))?(?:\+[0-9A-Za-z.-]+)?$");Version v;if(!m.Success||!Version.TryParse(string.Join(".",m.Groups[1].Value,m.Groups[2].Value,m.Groups[3].Value),out v))return null;return new ReleaseVersion{Tag=tag,number=v,suffix=m.Groups[4].Success?m.Groups[4].Value.Split('.'):new string[0]};}
        public int CompareTo(ReleaseVersion b){int c=number.CompareTo(b.number);if(c!=0)return c;if(suffix.Length==0||b.suffix.Length==0)return (b.suffix.Length>0?1:0)-(suffix.Length>0?1:0);
            for(int i=0;i<Math.Min(suffix.Length,b.suffix.Length);i++){string x=suffix[i],y=b.suffix[i];bool nx=Regex.IsMatch(x,"^[0-9]+$"),ny=Regex.IsMatch(y,"^[0-9]+$");c=nx!=ny?(nx?-1:1):nx&&x.Length!=y.Length?x.Length.CompareTo(y.Length):string.CompareOrdinal(x,y);if(c!=0)return c;}return suffix.Length.CompareTo(b.suffix.Length);}
    }
}
