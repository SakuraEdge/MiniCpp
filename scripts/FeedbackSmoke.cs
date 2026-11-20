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
    [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr hwnd,int msg,IntPtr w,IntPtr l);
    static void ClickGutter(Scintilla e,int line,int x){int y=e.PointYFromPosition(e.Lines[line].Position)+e.Lines[line].Height/2;var point=(IntPtr)((y<<16)|x);SendMessage(e.Handle,0x201,(IntPtr)1,point);SendMessage(e.Handle,0x202,IntPtr.Zero,point);Application.DoEvents();}
    const BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    static object Field(object o,string name){return o.GetType().GetField(name,F).GetValue(o);}
    static object Call(object o,string name,params object[] args){return o.GetType().GetMethods(F).First(m=>m.Name==name&&m.GetParameters().Length==args.Length).Invoke(o,args);}
    static void Check(bool ok,string why){if(!ok)throw new Exception(why);}
    [STAThread] static int Main(string[] args){try{
        Environment.SetEnvironmentVariable("MINICPP_HOME",Path.Combine(args[0],"profile"));Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
        string file=Path.Combine(args[0],"中文 example.cpp");File.WriteAllText(file,"int main() {}\r\n",new UTF8Encoding(false));
        var type=Assembly.LoadFrom(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"MiniCpp.exe")).GetType("MiniCpp.MainForm");
        using(var form=(Form)Activator.CreateInstance(type,new object[]{new[]{file}})){
            form.Opacity=0;form.ShowInTaskbar=false;form.Show();Application.DoEvents();form.WindowState=FormWindowState.Normal;form.Width=(int)Call(form,"Px",520);Application.DoEvents();
            var tools=(ToolStrip)Field(form,"tools");foreach(var name in new[]{"compile","run","compile-and-run","rebuild"}){var b=tools.Items[name];Check(b!=null&&b.Placement==ToolStripItemPlacement.Main&&tools.ClientRectangle.Contains(b.Bounds)&&b.Width>=(int)Call(form,"Px",36),"toolbar clipped or too small: "+name);}
            var split=(SplitContainer)Field(form,"horizontal");int before=split.Panel2.Height;Call(form,"ToggleOutput");Check(split.Panel2Collapsed,"collapse failed");Call(form,"ToggleOutput");Check(!split.Panel2Collapsed&&Math.Abs(before-split.Panel2.Height)<=2,"restore height changed");Call(form,"ToggleOutput");
            var doc=Field(form,"current");var edit=(Scintilla)Field(doc,"Edit");string code="void a(){\r\n    int n = 1 // 分号；\r\n    return;\r\n}\r\nvoid b(){\r\n    missing = 1;\r\n}\r\nint main(){\r\n    const char *text=R\"(正常；，。)\";\r\n    return 0；\r\n}\r\n";edit.Text=code;
            ClickGutter(edit,1,edit.Margins[0].Width+edit.Margins[1].Width/2);Check((edit.Lines[1].MarkerGet()&1)!=0,"click on line number did not set breakpoint");ClickGutter(edit,1,edit.Margins[0].Width/2);Check((edit.Lines[1].MarkerGet()&1)==0,"click on dot did not clear breakpoint");
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
