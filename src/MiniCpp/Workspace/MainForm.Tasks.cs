using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MiniCpp {
    internal sealed partial class MainForm {
        void MakeDebug(){
            var top=new Panel{Dock=DockStyle.Top,Height=72,Padding=new Padding(10,8,8,4),BackColor=Ui.Soft};
            stateLabel.Dock=DockStyle.Top;stateLabel.Height=25;stateLabel.ForeColor=Ui.Accent;frameLabel.Dock=DockStyle.Fill;frameLabel.AutoEllipsis=true;frameLabel.ForeColor=Ui.Muted;top.Controls.Add(frameLabel);top.Controls.Add(stateLabel);
            var actions=Ui.Strip(Ui.Soft,false);actions.Dock=DockStyle.Bottom;
            actions.Items.Add(Ui.Button("step-forward","单步 · F8",()=>DebugCmd(false)));actions.Items.Add(Ui.Button("play","继续 · F5",()=>DebugCmd(true)));actions.Items.Add(Ui.Button("pause","暂停",PauseDebug));actions.Items.Add(Ui.Button("square","停止调试 · Shift+F5",Stop));
            ConfigureList(locals,new[]{"变量","类型","当前值"},new[]{76,74,175});locals.Font=Ui.Mono;locals.ShowItemToolTips=true;
            vertical.Panel2.Controls.Add(locals);vertical.Panel2.Controls.Add(actions);vertical.Panel2.Controls.Add(top);
        }
        void OnUi(Action a){if(closing||IsDisposed||!IsHandleCreated)return;try{BeginInvoke((MethodInvoker)delegate{if(!closing&&!IsDisposed)a();});}catch(InvalidOperationException){}}
        bool Ready(){if(current==null)return false;if(owner!=null){Say("当前任务尚未结束："+owner.Name);return false;}if(string.IsNullOrEmpty(bin)||!File.Exists(Path.Combine(bin,"g++.exe"))){bin=Compiler.FindBin(prefs.Bin);if(bin==null){Error("没有找到编译器。请使用包含工具链的便携包，或在「工具 → 编译工具链」选择含 g++.exe 的 bin 目录。");return false;}}return true;}
        void BeginWork(SourceDocument d){owner=d;jobToken=new CancellationTokenSource();d.Edit.ReadOnly=true;}
        void EndWork(SourceDocument d){if(owner!=d)return;owner=null;if(jobToken!=null){jobToken.Dispose();jobToken=null;}if(!closing&&!d.Edit.IsDisposed){d.Edit.ReadOnly=false;RefreshDoc();}}
        void UpdateDoc(SourceDocument d,int view){if(!closing&&current==d){RefreshDoc();if(view>=0)View(view);}}
        async void StartBuild(bool run,bool rebuild,bool withDebugger){
            if(!Ready())return;var d=current;
            if(!Save(d,false,true))return;BeginWork(d);bool handedToDebugger=false;
            try{d.ClearErrorLines();d.Problems=new List<Diagnostic>();if(!withDebugger)d.BuildFailed=true;d.Status=withDebugger?"正在编译调试副本…":"正在编译…";d.LogText=d.Status;UpdateDoc(d,1);
                var result=await Compiler.BuildAsync(d.FullPath,d.Standard,d.O2,withDebugger,rebuild,bin,jobToken.Token);if(closing)return;
                d.LogText=result.Log;d.Problems=result.Diagnostics;if(!withDebugger){d.BuildFailed=!result.Success;if(result.Success)d.BuiltSource=d.Edit.Text;}
                if(!result.Success){d.MarkCompileErrors(result.Diagnostics);d.Status="编译失败";if(current==d&&horizontal.Panel2Collapsed)SetOutputVisible(true,false);UpdateDoc(d,2);return;}d.Status="编译成功";UpdateDoc(d,1);
                if(withDebugger){await StartDebugger(d,result.Executable);handedToDebugger=debug!=null;}
                else if(run)await RunProgram(d,result.Executable);
            }catch(OperationCanceledException){d.Status="已停止编译或运行";}
            catch(Exception ex){d.Status="任务失败";d.LogText+="\r\n"+ex.Message;UpdateDoc(d,1);if(debug!=null){var g=debug;debug=null;g.Dispose();vertical.Panel2Collapsed=true;}if(!closing)Error(ex.Message);}
            finally{if(!handedToDebugger)EndWork(d);}
        }
        async Task RunProgram(SourceDocument d,string exe){
            d.SetRecord(new RunRecord("正在命令行中运行…"));d.Status="命令行运行中";UpdateDoc(d,0);
            var token=jobToken.Token;string code=d.BuiltSource??d.Edit.Text,source=d.FullPath,toolsPath=bin;var mode=d.IO;
            try{
                var record=await Task.Run(()=>RunSession.LaunchAsync(exe,source,toolsPath,RunSession.FileMode(code,mode),token),token);
                if(closing||IsDisposed){record.Dispose();return;}d.SetRecord(record);d.Status=record.Aborted?"运行中止":"运行结束";UpdateDoc(d,0);
            }catch(OperationCanceledException){d.SetRecord(new RunRecord("运行已中止。",true));throw;}
            catch(Exception ex){d.SetRecord(new RunRecord("未能完成本次运行："+ex.Message));throw;}
        }
        async void RunOnly(){if(!Ready())return;var d=current;var exe=d.FullPath==null?null:Compiler.ArtifactPath(d.FullPath,false);if(d.BuildFailed||exe==null||!File.Exists(exe)){Say(d.BuildFailed?"上次编译失败，未运行旧程序":"尚无可执行文件，请先编译");return;}BeginWork(d);try{await RunProgram(d,exe);}catch(OperationCanceledException){d.Status="运行已停止";}catch(Exception ex){d.Status="运行失败";if(!closing)Error(ex.Message);}finally{EndWork(d);}}
        void StartOrContinue(){if(debug==null){StartBuild(false,false,true);return;}DebugCmd(true);}
        async Task StartDebugger(SourceDocument d,string exe){var g=new DebugSession(bin,exe,d.FullPath){LiveSampling=prefs.Sampling};debug=g;snapshot=null;debuggerRunning=false;locals.Items.Clear();vertical.Panel2Collapsed=false;
            vertical.SplitterDistance=Math.Max(Px(160),vertical.Width-Math.Min(Px(315),vertical.Width/3));stateLabel.Text="调试器 · 正在启动";frameLabel.Text="";d.SetRecord(new RunRecord("调试输入输出使用独立命令行。"));d.Status="正在启动 GDB";UpdateDoc(d,0);
            g.Log+=msg=>OnUi(()=>{if(debug!=g)return;if(d.LogText.Length<131072)d.LogText+="\r\n"+msg;if(current==d&&selectedView==1)log.Text=d.LogText;});
            g.Running+=()=>OnUi(()=>{if(debug!=g)return;debuggerRunning=true;d.Execution(0);d.Status="调试运行中";stateLabel.Text=snapshot==null?"调试器 · 运行中":"调试器 · 运行中 / 最近快照";if(current==d)RefreshStatus();});
            g.Stopped+=value=>OnUi(()=>{if(debug!=g)return;snapshot=value;debuggerRunning=value.Sample;d.Status=value.Sample?"调试运行中":"调试暂停";
                stateLabel.Text=value.Sample?"调试器 · 最近采样":"调试器 · 已暂停";frameLabel.Text=value.Function+"()   "+Path.GetFileName(value.File)+":"+value.Line;
                locals.BeginUpdate();locals.Items.Clear();foreach(var v in value.Variables){var r=new ListViewItem(v.Name);r.SubItems.Add(v.Type);r.SubItems.Add(v.Value);r.ToolTipText=v.Type+" "+v.Name+" = "+v.Value;locals.Items.Add(r);}locals.EndUpdate();
                if(!value.Sample){if(current!=d)Activate(d);if(string.Equals(value.File.Replace('/','\\'),d.FullPath,StringComparison.OrdinalIgnoreCase))d.Execution(value.Line);else d.Execution(0);}if(current==d)RefreshStatus();});
            g.Ended+=reason=>OnUi(()=>{if(debug==g)FinishDebugger(d,reason);});
            await g.StartAsync(d.Breakpoints().ToList());
        }
        string VariableValue(SourceDocument d,string name){if(debug==null||owner!=d||snapshot==null)return null;var v=snapshot.Variables.FirstOrDefault(x=>x.Name==name);return v==null?null:(debuggerRunning?"最近采样：":"当前值：")+v.Type+" "+v.Name+" = "+v.Value;}
        async void Breakpoint(SourceDocument d,int line,bool enabled){if(debug!=null&&owner==d)try{await debug.SetBreakpointAsync(line,enabled);}catch(Exception ex){if(enabled)d.Edit.Lines[line-1].MarkerDelete(0);else d.Edit.Lines[line-1].MarkerAdd(0);Say(ex.Message);}}
        async void DebugCmd(bool resume){var g=debug;if(g==null||g.IsRunning)return;try{if(resume)await g.ContinueAsync();else await g.StepAsync();}catch(Exception ex){if(g==debug)Say(ex.Message);}}
        async void PauseDebug(){var g=debug;if(g==null)return;try{await g.PauseAsync();}catch(Exception ex){if(debug==g)Say(ex.Message);}}
        void FinishDebugger(SourceDocument d,string why){var g=debug;debug=null;snapshot=null;debuggerRunning=false;if(g!=null)g.Dispose();d.Execution(0);d.Edit.CallTipCancel();locals.Items.Clear();vertical.Panel2Collapsed=true;d.Status=why;EndWork(d);}
        void Stop(){if(debug!=null&&owner!=null){FinishDebugger(owner,"已停止调试");return;}if(jobToken!=null){jobToken.Cancel();if(owner!=null){owner.Status="正在停止…";UpdateDoc(owner,-1);}}}
    }
}
