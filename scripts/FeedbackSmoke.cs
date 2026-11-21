// One focused Windows/WinForms smoke for the desktop feedback. Not part of the IDE.
using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using ScintillaNET;

internal static class FeedbackSmoke {
    [DllImport("user32.dll",EntryPoint="SendMessageW",ExactSpelling=true)] static extern IntPtr SendMessage(IntPtr hwnd,int msg,IntPtr w,IntPtr l);
    static void ClickGutter(Control gutter,Scintilla e,int line){int y=e.PointYFromPosition(e.Lines[line].Position)+e.Lines[line].Height/2;var point=(IntPtr)((y<<16)|(gutter.Width/2));SendMessage(gutter.Handle,0x201,(IntPtr)1,point);SendMessage(gutter.Handle,0x202,IntPtr.Zero,point);Application.DoEvents();}
    const BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    static object Field(object o,string name){return o.GetType().GetField(name,F).GetValue(o);}
    static object Call(object o,string name,params object[] args){return o.GetType().GetMethods(F).First(m=>m.Name==name&&m.GetParameters().Length==args.Length).Invoke(o,args);}
    static void Check(bool ok,string why){if(!ok)throw new Exception(why);}
    static void TypeChar(Scintilla e,char ch){SendMessage(e.Handle,0x102,(IntPtr)ch,IntPtr.Zero);}
    static int SavedPoints(string key){return (int)System.Xml.Linq.XElement.Load(Path.Combine(Environment.GetEnvironmentVariable("MINICPP_HOME"),"preferences.xml")).Attribute(key);}
    static Form Open(Type type,string[] paths){var f=(Form)Activator.CreateInstance(type,new object[]{paths});f.Opacity=0;f.ShowInTaskbar=false;f.Show();Application.DoEvents();return f;}
    static void CheckFonts(Type type,Form form,string file){
        var doc=Field(form,"current");var e=(Scintilla)Field(doc,"Edit");var prefs=Field(form,"prefs");var sizes=(int[])Field(prefs,"FontSizes");
        Check(sizes[1]==11&&e.Styles[Style.Default].Size==11,"default code size");
        // A second window started before the change still holds the old values.
        var stale=Open(type,new string[0]);
        for(int i=0;i<3;i++)SendMessage(e.Handle,0x20A,(IntPtr)((120<<16)|8),IntPtr.Zero); // Ctrl + wheel up
        Application.DoEvents();
        Check(sizes[1]==14&&e.Zoom==0&&e.Styles[Style.Default].Size==14,"Ctrl+wheel not folded into the setting: "+sizes[1]+" zoom "+e.Zoom);
        Check(SavedPoints("sourcePt")==14,"wheel size not written to disk");
        var stalePrefs=Field(stale,"prefs");stalePrefs.GetType().GetField("LargeTools",F).SetValue(stalePrefs,true);
        stale.Close();stale.Dispose();Application.DoEvents();
        Check(SavedPoints("sourcePt")==14,"another window overwrote the size on exit");
        Check((bool)System.Xml.Linq.XElement.Load(Path.Combine(Environment.GetEnvironmentVariable("MINICPP_HOME"),"preferences.xml")).Attribute("largeTools"),"other window's own change lost");
        sizes[1]=18;Call(form,"ApplyMetrics");Check((bool)Call(prefs,"Save")&&SavedPoints("sourcePt")==18,"dialog size not written to disk");
        form.Close();Application.DoEvents();
        using(var again=Open(type,new[]{file})){
            var d=Field(again,"current");var edit=(Scintilla)Field(d,"Edit");var number=(System.Drawing.Font)Field(Field(d,"Gutter"),"numberFont");
            Check(((int[])Field(Field(again,"prefs"),"FontSizes"))[1]==18&&edit.Zoom==0&&edit.Styles[Style.Default].Size==18&&edit.Styles[Style.Cpp.Word].Size==18,"size reverted after reopening: "+edit.Styles[Style.Default].Size);
            Check(Math.Abs(number.SizeInPoints-18)<0.6,"line numbers did not follow the code size");
            again.Close();
        }
        Console.WriteLine("PASS: Ctrl+wheel 11 -> 14 pt saved, kept when another window exits, dialog 18 pt restored for code and line numbers after reopening.");
    }
    static void CheckUnicode(Form form,string path){
        var doc=Field(form,"current");var e=(Scintilla)Field(doc,"Edit");
        Call(doc,"ApplyDisplay",96,11);Check(e.CaretWidth==2,"caret at 100%");
        Call(doc,"ApplyDisplay",192,11);Check(e.CaretWidth==3,"caret at 200%");
        Check(e.Technology==Technology.DirectWrite,"font fallback renderer");
        const string prefix="// 中文",face="\U0001F600",coder="\U0001F469\U0001F3FD\u200D\U0001F4BB",tail=" END\r\nint a;";
        e.Text=prefix+tail;e.SetEmptySelection(prefix.Length);e.EmptyUndoBuffer();
        foreach(char unit in face)TypeChar(e,unit);
        Check(e.Text==prefix+face+tail,"WM_CHAR pair: "+BitConverter.ToString(Encoding.UTF8.GetBytes(e.Text)));e.Undo();Check(e.Text==prefix+tail,"undo must remove the entire scalar");e.Redo();
        SendMessage(e.Handle,0x100,(IntPtr)Keys.Left,IntPtr.Zero); // native editor consumed a key before the picker committed
        Check(SendMessage(e.Handle,0x109,(IntPtr)0xffff,IntPtr.Zero)==(IntPtr)1,"WM_UNICHAR probe");
        for(int i=0;i<coder.Length;i++){int scalar=char.ConvertToUtf32(coder,i);SendMessage(e.Handle,0x109,(IntPtr)scalar,IntPtr.Zero);if(scalar>0xffff)i++;}
        string expected=prefix+coder+face+tail;Check(e.Text==expected,"composed sequence/WM_UNICHAR");
        int at=expected.IndexOf("END",StringComparison.Ordinal);e.SetSel(at,at+3);Check(e.SelectedText=="END","selection after supplementary characters");
        e.ReplaceSelection("ABC");Check(e.Text==expected.Replace("END","ABC"),"selection replacement");e.Undo();
        e.ReadOnly=true;foreach(char unit in face)TypeChar(e,unit);e.ReadOnly=false;Check(e.Text==expected,"undo or read-only input");
        Check((bool)Call(form,"Save",doc,false,false),"save");Check(File.ReadAllBytes(path).SequenceEqual(new UTF8Encoding(false,true).GetBytes(expected)),"UTF-8 bytes on disk");
        Call(form,"CloseDoc",doc);doc=Call(form,"OpenPath",path);Check(((Scintilla)Field(doc,"Edit")).Text==expected,"reopen");
        Console.WriteLine("PASS: DPI caret, Unicode typing, joined emoji sequence, selection, undo/redo, read-only, UTF-8 save/reopen.");
    }
    static void CheckEditor(Form form){
        var doc=Field(form,"current");var e=(Scintilla)Field(doc,"Edit");var gutter=(Control)Field(doc,"Gutter");
        const string code="#include <iostream>\r\nusing namespace std;\r\nint main() {\r\n    return 0;\r\n}";
        e.Text=code;e.Colorize(0,-1);Application.DoEvents();
        foreach(string token in new[]{"#include","<iostream>"}){int st=e.GetStyleAt(code.IndexOf(token,StringComparison.Ordinal));var color=e.Styles[st].ForeColor;Check(st==Style.Cpp.Preprocessor&&color.G>color.R,"directive not green: "+token);}
        foreach(string token in new[]{"using","namespace","int main","return"})Check(e.Styles[e.GetStyleAt(code.IndexOf(token,StringComparison.Ordinal))].Bold,"keyword not bold: "+token);
        foreach(char op in "();{}") {int st=e.GetStyleAt(code.IndexOf(op));var color=e.Styles[st].ForeColor;Check(st==Style.Cpp.Operator&&color.R>color.G,"operator not red: "+op);}
        Check(!e.Styles[e.GetStyleAt(code.IndexOf("main",StringComparison.Ordinal))].Bold,"main styled as a keyword");
        ClickGutter(gutter,e,3);Check((e.Lines[3].MarkerGet()&1)!=0,"center click missed line 4");
        int y=e.PointYFromPosition(e.Lines[3].Position)+e.Lines[3].Height/2;
        using(var bmp=new System.Drawing.Bitmap(gutter.Width,gutter.Height)){gutter.DrawToBitmap(bmp,gutter.ClientRectangle);int red=0;for(int x=0;x<bmp.Width;x++){var pixel=bmp.GetPixel(x,y);if(pixel.R>pixel.G+50)red++;}Check(red>2,"breakpoint not drawn on number row");}
        ClickGutter(gutter,e,3);Check((e.Lines[3].MarkerGet()&1)==0,"second click did not remove breakpoint");
        e.Text="";TypeChar(e,'(');TypeChar(e,'[');Check(e.Text=="([])"&&e.CurrentPosition==2,"nested pair insertion: "+e.Text);TypeChar(e,']');TypeChar(e,')');Check(e.Text=="([])"&&e.CurrentPosition==4,"closer duplicated");
        e.Text="";TypeChar(e,'{');Check(e.Text=="{}"&&e.CurrentPosition==1,"brace pair missing");
        e.Text="";TypeChar(e,'\"');TypeChar(e,'(');TypeChar(e,'\"');Check(e.Text=="\"(\""&&e.CurrentPosition==3,"literal pairing: "+e.Text);
        e.Text="#include ";e.GotoPosition(e.TextLength);TypeChar(e,'<');Check(e.Text=="#include <>","header pair missing");
        e.Text="a ";e.GotoPosition(e.TextLength);TypeChar(e,'<');Check(e.Text=="a <","comparison wrongly paired");
        e.EolMode=Eol.CrLf;e.Text="// before\r\n\r\n// after";e.SetEmptySelection(e.Lines[1].Position);
        foreach(char ch in "/solve")TypeChar(e,ch);
        string typed=e.Text;Check(typed=="// before\r\n/solve\r\n// after","template expanded before Enter");
        SendMessage(e.Handle,0x100,(IntPtr)Keys.Enter,IntPtr.Zero);TypeChar(e,'\r');
        string expected="// before\r\n#include <bits/stdc++.h>\r\nusing namespace std;\r\n\r\nvoid solve() {\r\n    \r\n}\r\n\r\nint main() {\r\n    solve();\r\n    return 0;\r\n}\r\n// after";
        Check(e.Text==expected,"incorrect solve template: "+e.Text.Replace("\r","<CR>").Replace("\n","<LF>"));
        Check(e.CurrentPosition==expected.IndexOf("    \r\n",StringComparison.Ordinal)+4,"solve caret: "+e.CurrentPosition);
        e.Undo();Check(e.Text==typed,"one undo should restore the command");
        e.SetSavePoint();Console.WriteLine("PASS: editor feedback and /solve Enter expansion, empty body caret, single undo.");
    }
    [STAThread] static int Main(string[] args){try{
        Environment.SetEnvironmentVariable("MINICPP_HOME",Path.Combine(args[0],"profile"));Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
        string file=Path.Combine(args[0],"中文 example.cpp");File.WriteAllText(file,"int main() {}\r\n",new UTF8Encoding(false));
        var type=Assembly.LoadFrom(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"MiniCpp.exe")).GetType("MiniCpp.MainForm");
        using(var form=(Form)Activator.CreateInstance(type,new object[]{new[]{file}})){
            form.Opacity=0;form.ShowInTaskbar=false;form.Show();Application.DoEvents();form.WindowState=FormWindowState.Normal;form.Width=(int)Call(form,"Px",520);Application.DoEvents();
            if(args.Length>1&&args[1]=="unicode"){CheckUnicode(form,file);form.Close();return 0;}
            if(args.Length>1&&args[1]=="fonts"){CheckFonts(type,form,file);return 0;}
            if(args.Length>1&&args[1]=="editor"){CheckEditor(form);form.Close();return 0;}
            var tools=(ToolStrip)Field(form,"tools");foreach(var name in new[]{"compile","run","compile-and-run","rebuild"}){var b=tools.Items[name];Check(b!=null&&b.Placement==ToolStripItemPlacement.Main&&tools.ClientRectangle.Contains(b.Bounds)&&b.Width>=(int)Call(form,"Px",36),"toolbar clipped or too small: "+name);}
            var split=(SplitContainer)Field(form,"horizontal");int before=split.Panel2.Height;Call(form,"ToggleOutput");Check(split.Panel2Collapsed,"collapse failed");Call(form,"ToggleOutput");Check(!split.Panel2Collapsed&&Math.Abs(before-split.Panel2.Height)<=2,"restore height changed");Call(form,"ToggleOutput");
            var doc=Field(form,"current");var edit=(Scintilla)Field(doc,"Edit");string code="void a(){\r\n    int n = 1 // 分号；\r\n    return;\r\n}\r\nvoid b(){\r\n    missing = 1;\r\n}\r\nint main(){\r\n    const char *text=R\"(正常；，。)\";\r\n    return 0；\r\n}\r\n";edit.Text=code;
            var gutter=(Control)Field(doc,"Gutter");ClickGutter(gutter,edit,1);Check((edit.Lines[1].MarkerGet()&1)!=0,"click on line number did not set breakpoint");ClickGutter(gutter,edit,1);Check((edit.Lines[1].MarkerGet()&1)==0,"click on dot did not clear breakpoint");
            var standard=(ToolStripDropDownButton)Field(form,"language");standard.DropDownItems.Cast<ToolStripMenuItem>().Single(i=>i.Text=="C++17").PerformClick();var toggle=(ToolStripButton)Field(form,"optimize");if(!toggle.Checked)toggle.PerformClick();
            Call(form,"StartBuild",false,false,false);var sw=Stopwatch.StartNew();while(Field(form,"owner")!=null&&sw.ElapsedMilliseconds<30000){Application.DoEvents();Thread.Sleep(15);}
            string log=(string)Field(doc,"LogText"),view=((RichTextBox)Field(form,"issues")).Text;var rows=((IEnumerable)Field(doc,"Problems")).Cast<object>().ToArray();
            Check(Field(form,"owner")==null&&(bool)Field(doc,"BuildFailed")&&File.ReadAllText(file)==code&&!edit.Modified,"auto-save/build did not finish");Check(!split.Panel2Collapsed&&(int)Field(form,"selectedView")==2,"issues not revealed");
            Check(rows.Any(r=>(int)Field(r,"HintLine")==2&&(int)Field(r,"HintColumn")==14&&((string)Field(r,"Suggestion")??"").Contains("分号")),"missing-semicolon location: "+view);
            Check(rows.Any(r=>(int)Field(r,"HintLine")==6&&((string)Field(r,"Suggestion")??"").Contains("没有声明「missing」")),"undeclared name: "+view);
            Check(rows.Count(r=>((string)Field(r,"Suggestion")??"").Contains("中文标点"))==1&&view.Contains("第 10 行，第 13 列")&&view.Contains("半角字符「;」")&&log.Contains("中文修改建议")&&log.Contains("was not declared"),"punctuation or visible log: "+view+"\n"+log);
            Check(log.Contains("-std=c++17 -O2"),"toolbar options not passed to GCC");var rich=(RichTextBox)Field(form,"issues");rich.Select(0,1);Check(Math.Abs(rich.SelectionFont.SizeInPoints-13)<0.6,"default diagnostic font too small");var pref=Field(form,"prefs");((int[])Field(pref,"FontSizes"))[2]=16;Call(form,"ApplyMetrics");rich.Select(0,1);Check(Math.Abs(rich.SelectionFont.SizeInPoints-16)<0.6,"font change not applied to existing diagnostics");
            var ver=type.Assembly.GetType("MiniCpp.ReleaseVersion");var parse=ver.GetMethod("Parse");object current=parse.Invoke(null,new object[]{"v1.0.0-dev"});Check((int)Call(current,"CompareTo",parse.Invoke(null,new object[]{"v1.0.0"}))<0,"stable not newer than dev");
            form.Close();Console.WriteLine("PASS: native gutter clicks, compact toolbar, actual C++17 -O2 command, diagnostic font 13 -> 16 pt, release precedence.");return 0;
        }
    }catch(Exception e){Console.Error.WriteLine(e);return 1;}}
}
