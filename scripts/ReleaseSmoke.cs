using System;
using System.Collections;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml.Linq;

[assembly:System.Runtime.Versioning.TargetFramework(".NETFramework,Version=v4.8")]
internal static class ReleaseSmoke {
    const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
    static Assembly assembly;
    static object Invoke(Type type,object instance,string method,params object[] args){
        try{return type.GetMethod(method,Flags).Invoke(instance,args);}catch(TargetInvocationException ex){throw ex.InnerException;}
    }
    static object Call(string type,string method,params object[] args){return Invoke(assembly.GetType("MiniCpp."+type,true),null,method,args);}
    static object Field(object instance,string name){return instance.GetType().GetField(name,Flags).GetValue(instance);}
    static void Set(object instance,string name,object value){instance.GetType().GetField(name,Flags).SetValue(instance,value);}
    static void Need(bool condition,string message){if(!condition)throw new Exception(message);}
    static string Hash(string path){return (string)Call("UpdatePackage","HashFile",path);}
    static void CopyFiles(string source,string target){
        Directory.CreateDirectory(target);
        foreach(string file in Directory.GetFiles(source,"*",SearchOption.AllDirectories)){
            string relative=file.Substring(source.Length+1),dest=Path.Combine(target,relative);
            Directory.CreateDirectory(Path.GetDirectoryName(dest));File.Copy(file,dest,true);
        }
    }
    static void ExpectFailure(Action action,string name){bool failed=false;try{action();}catch(Exception){failed=true;}Need(failed,"Expected failure: "+name);}
    static void Capture(Form form,string run,string name){
        form.PerformLayout();Application.DoEvents();
        using(var bitmap=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(bitmap,new Rectangle(Point.Empty,bitmap.Size));bitmap.Save(Path.Combine(run,name+".png"));}
    }

    static void Templates(string run){
        var settings=Call("TemplateSettings","Load");var stale=Call("TemplateSettings","Load");
        Set(settings,"Initial","// 初始框架\nint main() { ${cursor}return 0; }\n");
        var commands=(IList)Field(settings,"Commands");
        var entry=Activator.CreateInstance(assembly.GetType("MiniCpp.CodeTemplate"),true);
        Set(entry,"Command","/fast");Set(entry,"Code","int flags = 1 | 2;\n${cursor}// 😀\n");commands.Add(entry);
        Invoke(settings.GetType(),settings,"Save");
        ExpectFailure(()=>Invoke(stale.GetType(),stale,"Save"),"stale template edit");
        var args=new object[]{"/fast","\r\n",null,0};
        Need((bool)Call("ContestTemplates","TryExpand",args),"custom command missing");
        Need((string)args[2]=="int flags = 1 | 2;\r\n// 😀\r\n"&&(int)args[3]=="int flags = 1 | 2;\r\n".Length,"custom template/caret mismatch");
        Need(!(bool)Call("ContestTemplates","TryExpand",new object[]{"/fas","\n",null,0}),"partial command expanded");
        Set(entry,"Command","/main");ExpectFailure(()=>Invoke(settings.GetType(),settings,"Save"),"duplicate command");Set(entry,"Command","/fast");
        using(var form=(Form)Activator.CreateInstance(assembly.GetType("MiniCpp.MainForm"),new object[]{new string[0]})){
            form.Show();Application.DoEvents();
            var doc=Field(form,"current");var edit=Field(doc,"Edit");
            Need(((string)edit.GetType().GetProperty("Text").GetValue(edit,null)).Contains("// 初始框架"),"initial template not applied");
            form.Close();
        }
        using(var dialog=(Form)Activator.CreateInstance(assembly.GetType("MiniCpp.TemplateSettingsDialog"),true)){
            dialog.Show();Application.DoEvents();
            var list=(ListBox)Field(dialog,"items");var code=(TextBox)Field(dialog,"code");
            list.SelectedIndex=1;code.Text="int saved_before_switch;";list.SelectedIndex=2;list.SelectedIndex=1;
            Need(code.Text=="int saved_before_switch;","switching templates lost edits");
            Invoke(dialog.GetType(),dialog,"ResetSelected");Capture(dialog,run,"templates");
            dialog.ClientSize=new Size(620,380);Application.DoEvents();Need(code.Width>150&&code.Height>100,"compact template dialog clipped");
            Capture(dialog,run,"templates-compact");
            dialog.Close();
        }
        Console.WriteLine("PASS: custom initial code, commands, literal |, cursor, duplicate and concurrent-save protection, template editor.");
    }

    static void UpdateLayout(string run){
        var latest=Call("ReleaseVersion","Parse","v1.0.3");Set(latest,"Notes","框架与快捷指令\r\n\r\n可编辑初始代码、新增快捷指令。\r\n下载更新后自动重启，并重新打开已保存源码。");
        using(var dialog=(Form)Activator.CreateInstance(assembly.GetType("MiniCpp.UpdateDialog"),true)){
            Set(dialog,"checking",true);dialog.Show();Invoke(dialog.GetType(),dialog,"ShowRelease",latest);Capture(dialog,run,"update");
            dialog.ClientSize=new Size(510,370);dialog.PerformLayout();
            Need(((Control)Field(dialog,"notes")).Height>80,"update notes clipped at minimum size");
            var button=(Button)Field(dialog,"primary");Need(button.Text=="下载并更新"&&button.Enabled,"update action missing");
            Capture(dialog,run,"update-compact");dialog.Close();
        }
    }

    sealed class DownloadHandler:HttpMessageHandler {
        internal byte[] Zip,Descriptor;
        internal bool Corrupt;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token){
            token.ThrowIfCancellationRequested();
            bool descriptor=request.RequestUri.AbsolutePath.EndsWith("/update.json",StringComparison.Ordinal);
            byte[] data=descriptor?Descriptor:Zip;
            if(Corrupt&&!descriptor){data=(byte[])Zip.Clone();data[data.Length-1]^=0xff;}
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){RequestMessage=request,Content=new ByteArrayContent(data)});
        }
    }
    static void Download(string zip,string descriptor){
        var handler=new DownloadHandler{Zip=File.ReadAllBytes(zip),Descriptor=File.ReadAllBytes(descriptor)};
        using(var client=new HttpClient(handler)){
            var version=Call("ReleaseVersion","Parse","v1.0.2");
            var task=(Task)Call("UpdateDownload","PrepareWithClientAsync",version,null,CancellationToken.None,client);task.GetAwaiter().GetResult();
            Need(task.GetType().GetProperty("Result").GetValue(task,null)!=null,"download did not prepare update");
            handler.Corrupt=true;
            ExpectFailure(()=>((Task)Call("UpdateDownload","PrepareWithClientAsync",version,null,CancellationToken.None,client)).GetAwaiter().GetResult(),"bad SHA-256");
            using(var cancel=new CancellationTokenSource()){cancel.Cancel();ExpectFailure(()=>((Task)Call("UpdateDownload","PrepareWithClientAsync",version,null,cancel.Token,client)).GetAwaiter().GetResult(),"cancelled download");}
        }
        Console.WriteLine("PASS: download pipeline, integrity rejection and cancellation.");
    }

    static void Transaction(string run,string zip){
        string stage=Path.Combine(run,"stage"),target=Path.Combine(run,"target");
        Call("UpdatePackage","Extract",zip,stage,"1.0.2");CopyFiles(stage,target);
        File.WriteAllText(Path.Combine(target,"keep.cpp"),"user source");File.WriteAllText(Path.Combine(target,"portable.mode"),"");
        string exe=Path.Combine(target,"MiniCpp.exe"),license=Path.Combine(target,"LICENSE");
        File.WriteAllText(license,"old license");string old=Hash(license);
        using(var locked=new FileStream(exe,FileMode.Open,FileAccess.Read,FileShare.Read))ExpectFailure(()=>Call("UpdateTransaction","Apply",stage,target,"1.0.2"),"locked executable");
        Need(Hash(license)==old,"failed preflight changed files");
        File.Delete(license);Directory.CreateDirectory(license);string oldReadme="old readme";File.WriteAllText(Path.Combine(target,"README.md"),oldReadme);
        ExpectFailure(()=>Call("UpdateTransaction","Apply",stage,target,"1.0.2"),"mid-update directory conflict");
        Need(File.ReadAllText(Path.Combine(target,"README.md"))==oldReadme,"rollback lost original README");
        Directory.Delete(license);File.WriteAllText(license,"old license");
        Call("UpdateTransaction","Apply",stage,target,"1.0.2");
        Need(Hash(exe)==Hash(Path.Combine(stage,"MiniCpp.exe")),"updated executable mismatch");
        Need(File.ReadAllText(Path.Combine(target,"keep.cpp"))=="user source"&&File.Exists(Path.Combine(target,"portable.mode")),"source or portable marker changed");
        Need(!File.Exists(Path.Combine(target,".minicpp-update.xml")),"successful update left recovery journal");

        string id=Guid.NewGuid().ToString("N"),backup=Path.Combine(target,".minicpp-backup-"+id);Directory.CreateDirectory(backup);
        File.Copy(license,Path.Combine(backup,"LICENSE"));string good=Hash(license);File.WriteAllText(license,"interrupted update");
        new XElement("UpdateTransaction",new XAttribute("id",id),new XElement("File",new XAttribute("path","LICENSE"),new XAttribute("existed",true),new XAttribute("sha256",good))).Save(Path.Combine(target,".minicpp-update.xml"));
        Call("UpdateTransaction","Recover",target);Need(Hash(license)==good,"interrupted transaction was not restored");
        string bad=Path.Combine(run,"bad.zip");
        using(var archive=ZipFile.Open(bad,ZipArchiveMode.Create)){archive.CreateEntry("../outside.exe");}
        ExpectFailure(()=>Call("UpdatePackage","Extract",bad,Path.Combine(run,"reject"),"1.0.2"),"ZIP traversal");
        Console.WriteLine("PASS: update apply, locks, partial-failure rollback, interrupted recovery, source/portable preservation and traversal rejection.");
    }

    static void UpdateHost(string run,string zip){
        string app=Path.Combine(run,"host-target"),stage=Path.Combine(run,"host-stage"),work=Path.Combine(run,"host-work");
        Directory.CreateDirectory(work);Call("UpdatePackage","Extract",zip,stage,"1.0.2");CopyFiles(stage,app);
        File.WriteAllText(Path.Combine(app,"LICENSE"),"old license");
        string source=Path.Combine(run,"reopen.cpp");File.WriteAllText(source,"int main() {}\n");
        File.Copy(Path.Combine(app,"MiniCpp.Updater.exe"),Path.Combine(work,"MiniCpp.Updater.exe"));File.Copy(zip,Path.Combine(work,"update.zip"));
        string request=Path.Combine(work,"request.xml");
        new XElement("Update",new XAttribute("target",app),new XAttribute("pid",int.MaxValue),new XAttribute("started",0),new XAttribute("version","1.0.2"),new XAttribute("sha256",Hash(zip)),new XElement("Source",source)).Save(request);
        var info=new ProcessStartInfo(Path.Combine(work,"MiniCpp.Updater.exe"),"\""+request+"\""){UseShellExecute=false,CreateNoWindow=true};
        using(var helper=Process.Start(info)){if(!helper.WaitForExit(30000)){helper.Kill();throw new Exception("Update helper timed out");}Need(helper.ExitCode==0,"update host failed");}
        Process editor=null;var timer=Stopwatch.StartNew();
        while(timer.ElapsedMilliseconds<15000&&editor==null){
            foreach(var candidate in Process.GetProcessesByName("MiniCpp")){
                if(string.Equals(candidate.MainModule.FileName,Path.Combine(app,"MiniCpp.exe"),StringComparison.OrdinalIgnoreCase))editor=candidate;else candidate.Dispose();
            }
            if(editor==null)Thread.Sleep(100);
        }
        Need(editor!=null,"updated editor did not restart");
        using(editor){
            timer.Restart();while(timer.ElapsedMilliseconds<10000){editor.Refresh();if(editor.MainWindowTitle.Contains("reopen.cpp"))break;Thread.Sleep(100);}
            Need(editor.MainWindowTitle.Contains("reopen.cpp"),"source tab not reopened");
            editor.CloseMainWindow();if(!editor.WaitForExit(5000))editor.Kill();
        }
        Need(Hash(Path.Combine(app,"LICENSE"))==Hash(Path.Combine(stage,"LICENSE")),"host did not update files");
        Console.WriteLine("PASS: separate update helper, file replacement, automatic restart and restored source tab.");
    }
    [STAThread] static int Main(string[] args){try{
        Need(args.Length==3,"Expected work directory, update ZIP and descriptor");
        string run=Path.GetFullPath(args[0]);Directory.CreateDirectory(run);Environment.SetEnvironmentVariable("MINICPP_HOME",Path.Combine(run,"profile"));
        Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
        assembly=Assembly.LoadFrom(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"MiniCpp.exe"));
        Templates(run);UpdateLayout(run);Download(args[1],args[2]);Transaction(run,args[1]);UpdateHost(run,args[1]);return 0;
    }catch(Exception ex){Console.Error.WriteLine(ex);return 1;}}
}
