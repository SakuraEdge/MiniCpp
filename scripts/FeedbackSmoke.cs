// One focused Windows/WinForms smoke for the 0.1.1 feedback. Not part of the IDE.
using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using ScintillaNET;

internal static class FeedbackSmoke {
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
            Call(form,"StartBuild",false,false,false);var sw=Stopwatch.StartNew();while(Field(form,"owner")!=null&&sw.ElapsedMilliseconds<30000){Application.DoEvents();Thread.Sleep(15);}
            string log=(string)Field(doc,"LogText"),view=((RichTextBox)Field(form,"issues")).Text;var rows=((IEnumerable)Field(doc,"Problems")).Cast<object>().ToArray();
            Check(Field(form,"owner")==null&&(bool)Field(doc,"BuildFailed")&&File.ReadAllText(file)==code&&!edit.Modified,"auto-save/build did not finish");Check(!split.Panel2Collapsed&&(int)Field(form,"selectedView")==2,"issues not revealed");
            Check(rows.Any(r=>(int)Field(r,"HintLine")==2&&(int)Field(r,"HintColumn")==14&&((string)Field(r,"Suggestion")??"").Contains("分号")),"missing-semicolon location: "+view);
            Check(rows.Any(r=>(int)Field(r,"HintLine")==6&&((string)Field(r,"Suggestion")??"").Contains("没有声明「missing」")),"undeclared name: "+view);
            Check(rows.Count(r=>((string)Field(r,"Suggestion")??"").Contains("中文标点"))==1&&view.Contains("第 10 行，第 13 列")&&view.Contains("半角字符「;」")&&log.Contains("中文修改建议")&&log.Contains("was not declared"),"punctuation or visible log: "+view+"\n"+log);
            form.Close();Console.WriteLine("PASS: narrow toolbar, panel collapse/restore, autosave to original Chinese path, real GCC and visible line-specific hints for all three CE cases.");return 0;
        }
    }catch(Exception e){Console.Error.WriteLine(e);return 1;}}
}
