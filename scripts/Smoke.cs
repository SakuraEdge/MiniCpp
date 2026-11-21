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
    static void Until(Func<bool> ready,string message){var t=Stopwatch.StartNew();while(!ready()&&t.ElapsedMilliseconds<5000){Application.DoEvents();Thread.Sleep(10);}Need(ready(),message);}
    [STAThread] public static int Main(string[] args){try{
        string app=AppDomain.CurrentDomain.BaseDirectory,workspace=Path.GetFullPath(args[0]),path=Path.Combine(workspace,"竞赛 样例.cpp");
        Environment.SetEnvironmentVariable("MINICPP_HOME",Path.Combine(workspace,"profile"));
        Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
        var assembly=Assembly.LoadFrom(Path.Combine(app,"MiniCpp.exe"));var type=assembly.GetType("MiniCpp.MainForm");
        using(var f=(Form)Activator.CreateInstance(type,new object[]{new string[0]})){
            f.ShowInTaskbar=false;f.Opacity=0;f.Show();Application.DoEvents();
            var original=Field(f,"current");var tabs=(ToolStrip)Field(f,"tabs");tabs.Items[tabs.Items.Count-1].PerformClick();var right=Field(f,"current");
            Call(f,"Activate",original);tabs.Items[tabs.Items.Count-1].PerformClick();var doc=Field(f,"current");var order=(IList)Field(f,"docs");Need(order.Count==3&&order[0]==original&&order[1]==doc&&order[2]==right,"new file did not open immediately after active tab");Need(Field(doc,"FullPath")==null,"draft unexpectedly has a path");
            var editor=(Scintilla)Field(doc,"Edit");const string example="#include <iostream>\r\n#include <utility>\r\n#include <string>\r\nint main() {\r\n    int a, b; std::cin >> a >> b;\r\n    const auto [x, y] = std::pair{a, b};\r\n    for(int i=0; i<12; ++i) std::cout << x+y+i << '\\n';\r\n    std::cerr << \"debug only\\n\";\r\n    std::string s; while(std::getline(std::cin,s)) {}\r\n    return 0;\r\n}\r\n";editor.Text=example;
            Need((bool)Call(f,"WriteDocument",doc,path)&&File.ReadAllText(path)==example,"save failed");((ToolStripDropDownButton)Field(f,"language")).DropDownItems.Cast<ToolStripMenuItem>().First(b=>b.Text=="C++17").PerformClick();((ToolStripButton)Field(f,"optimize")).Checked=true;
            Call(f,"StartBuild",false,false,false);var sw=Stopwatch.StartNew();while(Field(f,"owner")!=null&&sw.ElapsedMilliseconds<60000){Application.DoEvents();Thread.Sleep(10);}string log=(string)Field(doc,"LogText");Need(Field(f,"owner")==null&&!(bool)Field(doc,"BuildFailed")&&log.Contains("-std=c++17 -O2"),"C++17 build failed: "+log);
            string exe=(string)assembly.GetType("MiniCpp.Compiler").GetMethod("ArtifactPath").Invoke(null,new object[]{path,false});string report=Path.Combine(workspace,"run.xml");
            var pi=new ProcessStartInfo(Path.Combine(app,"MiniCpp.Console.exe"),string.Join(" ",new[]{exe,workspace,report,(string)Field(f,"bin"),"0"}.Select(Q))){UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true};
            string data="4 8\r\n"+new string('x',32762)+"\U0001F600尾\r\n\r\n",answers=string.Join("\r\n",Enumerable.Range(12,12))+"\r\n";
            using(var p=Process.Start(pi)){
                using(var w=new StreamWriter(p.StandardInput.BaseStream,new UTF8Encoding(false),1024,true){AutoFlush=true}){
                    w.WriteLine("4 8");var first=p.StandardOutput.ReadLineAsync();Need(first.Wait(10000)&&first.Result=="12"&&!File.Exists(report),"record published before EOF");w.Write(data.Substring(5));}
                p.StandardInput.Close();if(!p.WaitForExit(20000)){p.Kill();throw new Exception("console timed out");}
                Need(p.ExitCode==0&&p.StandardError.ReadToEnd().Trim()=="debug only","console exit/stderr mismatch");}
            var record=assembly.GetType("MiniCpp.RunRecord").GetMethod("Load").Invoke(null,new object[]{report});
            Call(doc,"SetRecord",record);Call(f,"OpenView",0);Call(f,"RefreshDoc");
            var panel=Field(f,"transcript");var input=(Control)Field(panel,"input");var outputPane=(Control)Field(panel,"output");
            var inText=(TextBox)Field(input,"text");var outText=(TextBox)Field(outputPane,"text");
            Until(()=>inText.Text.Length>0&&outText.Text.Contains("省略 8 行"),"input or folded output missing");
            Need(inText.Font.Size==14&&outText.Font.Size==14&&((ToolStripStatusLabel)Field(f,"status")).Text.Contains("退出码 0"),"font or exit status");
            string pageOne=inText.Text;((ToolStripButton)Field(input,"next")).PerformClick();Until(()=>inText.Text.Contains("尾"),"input page two");Need(pageOne+inText.Text==data,"long input, blank line or supplementary character lost");
            ((ToolStripButton)Field(outputPane,"fold")).PerformClick();Until(()=>outText.Text==answers,"expanded answers mismatch");
            ((ToolStripDropDownButton)Field(outputPane,"channel")).DropDownItems[1].PerformClick();Until(()=>outText.Text=="debug only\r\n","stderr not isolated");
            f.WindowState=FormWindowState.Maximized;Application.DoEvents();Need(outputPane.Left>input.Left,"wide panes not side by side");
            f.WindowState=FormWindowState.Normal;f.Width=(int)Call(f,"Px",500);Application.DoEvents();Need(outputPane.Top>input.Top,"narrow panes not stacked");
            ((int[])Field(Field(f,"prefs"),"FontSizes"))[4]=16;Call(f,"ApplyMetrics");Need(inText.Font.Size==16&&outText.Font.Size==16,"size did not update both panes");
            Call(f,"Activate",right);Call(f,"Activate",doc);Until(()=>outText.Text.Contains("省略 8 行"),"record lost on tab change");
            Call(f,"CloseDoc",doc);Need(!Directory.GetFiles(workspace,"run.xml*").Any(),"record files left on document close");
            f.Close();Console.WriteLine("PASS: C++17 -O2, delayed publication, independent input/stdout/stderr, paging, folding, wide/narrow layout, shared font and cleanup.");return 0;
        }
    }catch(Exception ex){Console.Error.WriteLine(ex);return 1;}}
}
