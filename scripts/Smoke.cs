// One integration path for the portable build. This file is not compiled into the IDE.
using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows.Forms;
using System.Xml.Linq;
using System.Linq;
using System.Threading;
using ScintillaNET;

internal static class Smoke {
    const BindingFlags Fields=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    static object Field(object o,string n){return o.GetType().GetField(n,Fields).GetValue(o);}
    static object Call(object o,string n,params object[] args){return o.GetType().GetMethods(Fields).First(m=>m.Name==n&&m.GetParameters().Length==args.Length).Invoke(o,args);}
    static void Need(bool b,string m){if(!b)throw new Exception(m);}
    static string Q(string s){return "\""+s+"\"";}
    [STAThread] public static int Main(string[] args){try{
        string app=AppDomain.CurrentDomain.BaseDirectory,workspace=Path.GetFullPath(args[0]),path=Path.Combine(workspace,"竞赛 样例.cpp");
        Environment.SetEnvironmentVariable("MINICPP_HOME",Path.Combine(workspace,"profile"));
        Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
        var assembly=Assembly.LoadFrom(Path.Combine(app,"MiniCpp.exe"));var type=assembly.GetType("MiniCpp.MainForm");
        using(var f=(Form)Activator.CreateInstance(type,new object[]{new string[0]})){
            f.ShowInTaskbar=false;f.Opacity=0;f.Show();Application.DoEvents();
            var original=Field(f,"current");var tabs=(ToolStrip)Field(f,"tabs");tabs.Items[tabs.Items.Count-1].PerformClick();var right=Field(f,"current");
            Call(f,"Activate",original);tabs.Items[tabs.Items.Count-1].PerformClick();var doc=Field(f,"current");var order=(IList)Field(f,"docs");Need(order.Count==3&&order[0]==original&&order[1]==doc&&order[2]==right,"new file did not open immediately after active tab");Need(Field(doc,"FullPath")==null,"draft unexpectedly has a path");
            var editor=(Scintilla)Field(doc,"Edit");const string example="#include <iostream>\r\n#include <utility>\r\n\r\nint main() {\r\n    int a, b;\r\n    std::cin >> a >> b;\r\n    const auto [x, y] = std::pair{a, b};\r\n    std::cout << x + y << '\\n';\r\n    return 0;\r\n}\r\n";editor.Text=example;
            Need((bool)Call(f,"WriteDocument",doc,path)&&File.ReadAllText(path)==example,"save failed");((ToolStripDropDownButton)Field(f,"language")).DropDownItems.Cast<ToolStripMenuItem>().First(b=>b.Text=="C++17").PerformClick();((ToolStripButton)Field(f,"optimize")).Checked=true;
            Call(f,"StartBuild",false,false,false);var sw=Stopwatch.StartNew();while(Field(f,"owner")!=null&&sw.ElapsedMilliseconds<60000){Application.DoEvents();Thread.Sleep(10);}string log=(string)Field(doc,"LogText");Need(Field(f,"owner")==null&&!(bool)Field(doc,"BuildFailed")&&log.Contains("-std=c++17 -O2"),"C++17 build failed: "+log);
            string exe=(string)assembly.GetType("MiniCpp.Compiler").GetMethod("ArtifactPath").Invoke(null,new object[]{path,false});string report=Path.Combine(workspace,"run.xml");
            var pi=new ProcessStartInfo(Path.Combine(app,"MiniCpp.Console.exe"),string.Join(" ",new[]{exe,workspace,report,(string)Field(f,"bin"),"0"}.Select(Q))){UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true};
            using(var p=Process.Start(pi)){p.StandardInput.WriteLine("4 8");p.StandardInput.Close();if(!p.WaitForExit(20000)){p.Kill();throw new Exception("console timed out");}string output=p.StandardOutput.ReadToEnd();Need(p.ExitCode==0&&output.Trim()=="12","console failed: "+output+" "+p.StandardError.ReadToEnd());}
            var xml=XElement.Load(report);Need((int)xml.Attribute("exit")==0&&!(bool)xml.Attribute("aborted")&&xml.Elements("row").Any(x=>(string)x.Attribute("kind")=="in"&&x.Value=="4 8")&&xml.Elements("row").Any(x=>(string)x.Attribute("kind")=="out"&&x.Value=="12"),"transcript mismatch: "+xml);
            f.Close();Console.WriteLine("PASS: new tab after active, UTF-8 save, GCC C++17 -O2, real child output 12 and transcript after exit.");return 0;
        }
    }catch(Exception ex){Console.Error.WriteLine(ex);return 1;}}
}
