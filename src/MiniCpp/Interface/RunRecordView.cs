using System;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MiniCpp {
    internal sealed class RunStreamPane : Panel {
        readonly bool input;
        readonly TextBox text=Ui.Viewer();
        readonly ToolStrip header=Ui.Strip(Ui.Soft,true);
        readonly ToolStripLabel title=new ToolStripLabel(), count=new ToolStripLabel();
        readonly ToolStripDropDownButton channel=new ToolStripDropDownButton();
        readonly ToolStripButton fold=new ToolStripButton(),previous=new ToolStripButton("‹"),next=new ToolStripButton("›");
        readonly ToolStripLabel pageInfo=new ToolStripLabel();
        RecordedStream stream;
        RunRecord record;
        long page;
        int request;
        bool expanded,showError;

        public RunStreamPane(bool isInput){
            input=isInput;text.AccessibleName=input?"完整输入记录":"程序输出记录";
            header.Dock=DockStyle.Top;header.GripStyle=ToolStripGripStyle.Hidden;
            channel.AccessibleName="选择输出流";channel.DropDownItems.Add("标准输出 · stdout",null,(s,e)=>Choose(false));
            channel.DropDownItems.Add("错误输出 · stderr",null,(s,e)=>Choose(true));
            foreach(var b in new[]{fold,previous,next})b.DisplayStyle=ToolStripItemDisplayStyle.Text;
            previous.ToolTipText=previous.AccessibleName="上一页";next.ToolTipText=next.AccessibleName="下一页";
            previous.Click+=(s,e)=>{if(page>0){page--;ReadPage();}};
            next.Click+=(s,e)=>{if(stream!=null&&page+1<stream.Pages){page++;ReadPage();}};
            fold.Click+=(s,e)=>{expanded=!expanded;page=0;ReadPage();};
            header.Items.AddRange(new ToolStripItem[]{title,channel,count,fold,previous,pageInfo,next});
            Controls.Add(text);Controls.Add(header);
        }
        public void Bind(RunRecord value){
            record=value;Choose(!input&&value!=null&&value.Error!=null&&value.Error.Chars>0&&value.Output.Chars==0);
        }
        void Choose(bool error){
            showError=error;stream=record==null?null:input?record.Input:error?record.Error:record.Output;
            page=0;expanded=input;ReadPage();
        }
        async void ReadPage(){
            int version=++request;var snapshot=stream;long index=page;
            bool useSummary=snapshot!=null&&snapshot.CanFold&&!expanded;
            bool choose=!input&&record!=null&&record.Error!=null&&record.Error.Chars>0;
            title.Text=input?"输入  stdin":"输出  stdout";title.Visible=!choose;channel.Visible=choose;
            channel.Text=showError?"错误输出  stderr":"输出  stdout";
            count.Text=(snapshot==null?0:snapshot.Lines)+" 行";
            fold.Visible=!input&&snapshot!=null&&snapshot.CanFold;
            fold.Text=useSummary?"展开":"收起";fold.ToolTipText=useSummary?"查看完整输出":"只看输出摘要";
            bool paginate=snapshot!=null&&!useSummary&&snapshot.Pages>1;
            previous.Visible=next.Visible=pageInfo.Visible=paginate;previous.Enabled=index>0;next.Enabled=paginate&&index+1<snapshot.Pages;
            pageInfo.Text=paginate?(index+1)+"/"+snapshot.Pages:"";text.Clear();
            if(snapshot==null||snapshot.Chars==0)return;
            string value;
            try{value=await Task.Run(()=>useSummary?snapshot.Summary():snapshot.Page(index));}
            catch(IOException ex){value="无法读取运行记录："+ex.Message;}
            catch(UnauthorizedAccessException ex){value="无法读取运行记录："+ex.Message;}
            if(version!=request||IsDisposed||Disposing)return;
            text.Text=value;text.Select(0,0);text.ScrollToCaret();
        }
        public bool HandleTextKey(Keys key){
            if(!text.Focused)return false;
            if(key==(Keys.Control|Keys.C)){text.Copy();return true;}
            if(key==(Keys.Control|Keys.A)){text.SelectAll();return true;}return false;
        }
        public void SetFonts(Font ui,Font mono,int dpi){header.Font=ui;text.Font=mono;
            header.Height=Math.Max(ui.Height+8*dpi/96,27*dpi/96);}
        public int MinimumReadableHeight {get{return header.Height+text.Font.Height+SystemInformation.HorizontalScrollBarHeight+6;}}
        public void ApplyPalette(Theme p){BackColor=text.BackColor=p.Paper;text.ForeColor=title.ForeColor=p.Ink;
            header.BackColor=p.Paper;header.ForeColor=p.Ink;count.ForeColor=p.Muted;channel.ForeColor=fold.ForeColor=p.Accent;
            channel.DropDown.BackColor=p.Soft;channel.DropDown.ForeColor=p.Ink;header.Invalidate();}
        protected override void Dispose(bool disposing){request++;base.Dispose(disposing);}
    }

    internal sealed class RunRecordView : Panel {
        readonly Panel scroll=new Panel{Dock=DockStyle.Fill,AutoScroll=true};
        readonly Panel body=new Panel();
        readonly RunStreamPane input=new RunStreamPane(true),output=new RunStreamPane(false);
        readonly Label message=new Label{Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft,AutoEllipsis=true};
        RunRecord record;int dpi=96;bool arranging,bound;
        public RunRecordView(){
            Dock=DockStyle.Fill;AccessibleName="运行输入与输出";
            body.Controls.Add(input);body.Controls.Add(output);scroll.Controls.Add(body);Controls.Add(scroll);Controls.Add(message);
            scroll.SizeChanged+=(s,e)=>Arrange();scroll.VisibleChanged+=(s,e)=>Arrange();Bind(null);
        }
        public void Bind(RunRecord value){
            if(bound&&record==value)return;bound=true;record=value;
            bool hasData=value!=null&&value.Input!=null;
            message.Text=hasData?"":value==null?"暂无运行记录":value.Message;
            message.Visible=!hasData;scroll.Visible=hasData;
            input.Bind(value);output.Bind(value);scroll.AutoScrollPosition=Point.Empty;Arrange();
        }
        void Arrange(){
            if(arranging||!scroll.Visible||scroll.ClientSize.Width<1)return;arranging=true;
            try{
                int w=scroll.ClientSize.Width,h=scroll.ClientSize.Height,gap=Math.Max(1,dpi/96);
                int windowWidth=FindForm()==null?w:FindForm().Width;
                bool narrow=w*96L<=520L*dpi || windowWidth<=Screen.FromControl(this).WorkingArea.Width*.53;
                int min=input.MinimumReadableHeight;
                int length=Math.Max(h,narrow?2*min+gap:min);
                scroll.AutoScrollMinSize=new Size(0,length);
                w=scroll.ClientSize.Width;body.Bounds=new Rectangle(scroll.AutoScrollPosition,new Size(w,length));
                int mid=narrow?(length-gap)/2:(w-gap)/2;
                input.Bounds=new Rectangle(0,0,narrow?w:mid,narrow?mid:length);
                output.Bounds=narrow?new Rectangle(0,mid+gap,w,length-mid-gap):new Rectangle(mid+gap,0,w-mid-gap,length);
            }finally{arranging=false;}
        }
        public void SetFonts(Font ui,Font mono,int deviceDpi){dpi=deviceDpi;message.Font=ui;input.SetFonts(ui,mono,dpi);output.SetFonts(ui,mono,dpi);Arrange();}
        public void ApplyPalette(Theme p){BackColor=scroll.BackColor=message.BackColor=p.Paper;body.BackColor=p.Line;message.ForeColor=p.Muted;input.ApplyPalette(p);output.ApplyPalette(p);}
        public bool HandleTextKey(Keys key){return input.HandleTextKey(key)||output.HandleTextKey(key);}
    }
}
