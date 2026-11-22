using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace MiniCpp {
    internal sealed class TemplateSettingsDialog:Form {
        readonly TemplateSettings settings=TemplateSettings.Load();
        readonly ListBox items=new ListBox{Dock=DockStyle.Fill,IntegralHeight=false,BorderStyle=BorderStyle.None};
        readonly TextBox command=new TextBox{Dock=DockStyle.Fill,MaxLength=65};
        readonly TextBox code=new TextBox{Dock=DockStyle.Fill,Multiline=true,AcceptsTab=true,AcceptsReturn=true,WordWrap=false,ScrollBars=ScrollBars.Both,Font=Ui.Mono,MaxLength=200000};
        readonly Label error=new Label{AutoSize=true,Dock=DockStyle.Fill,MaximumSize=new Size(750,0)};
        int selected=-1;bool loading;

        internal TemplateSettingsDialog(){
            Text="框架与快捷指令";Font=Ui.TextFont;AutoScaleDimensions=new SizeF(96,96);AutoScaleMode=AutoScaleMode.Dpi;
            ClientSize=new Size(800,510);MinimumSize=new Size(620,380);StartPosition=FormStartPosition.CenterParent;ShowInTaskbar=false;MinimizeBox=false;
            var layout=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=3,Padding=new Padding(16)};
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,174));layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            var left=new Panel{Dock=DockStyle.Fill,Padding=new Padding(0,0,12,0)};
            var bar=Ui.Strip(Ui.Soft,false);bar.Dock=DockStyle.Bottom;
            bar.Items.Add(Ui.Button("plus","新增快捷指令",AddCommand));bar.Items.Add(Ui.Button("x","删除快捷指令",RemoveCommand));
            left.Controls.Add(items);left.Controls.Add(bar);layout.Controls.Add(left,0,0);
            var right=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=4};
            right.RowStyles.Add(new RowStyle(SizeType.AutoSize));right.RowStyles.Add(new RowStyle(SizeType.AutoSize));right.RowStyles.Add(new RowStyle(SizeType.AutoSize));right.RowStyles.Add(new RowStyle(SizeType.Percent,100));
            right.Controls.Add(new Label{Text="调用指令",AutoSize=true},0,0);right.Controls.Add(command,0,1);
            right.Controls.Add(new Label{Text="框架代码    光标位置（可选）：${cursor}",AutoSize=true,Margin=new Padding(3,12,3,8)},0,2);right.Controls.Add(code,0,3);
            layout.Controls.Add(right,1,0);layout.Controls.Add(error,0,1);layout.SetColumnSpan(error,2);
            var actions=new FlowLayoutPanel{AutoSize=true,Dock=DockStyle.Fill,FlowDirection=FlowDirection.RightToLeft,Padding=new Padding(0,12,0,0)};
            var save=new Button{Text="保存",AutoSize=true};var cancel=new Button{Text="取消",AutoSize=true,DialogResult=DialogResult.Cancel};var reset=new Button{Text="恢复当前默认",AutoSize=true};
            save.Click+=(s,e)=>SaveChanges();reset.Click+=(s,e)=>ResetSelected();actions.Controls.AddRange(new Control[]{save,cancel,reset});
            layout.Controls.Add(actions,0,2);layout.SetColumnSpan(actions,2);Controls.Add(layout);CancelButton=cancel;
            items.SelectedIndexChanged+=(s,e)=>SelectItem();command.Leave+=(s,e)=>StoreCurrent();
            Theme.Dialog(this);items.BackColor=code.BackColor=Theme.Current.Paper;error.ForeColor=Theme.Current.Error;
            HandleCreated+=(s,e)=>Theme.CaptionTheme(this,Theme.Current.Amount>=.5);
            RefreshList(0);error.Text=settings.ReadError??"";
        }

        void StoreCurrent(){
            if(loading||selected<0)return;
            if(selected==0)settings.Initial=code.Text;
            else{var item=settings.Commands[selected-1];item.Command=command.Text.Trim();item.Code=code.Text;loading=true;try{items.Items[selected]=item;}finally{loading=false;}}
        }
        void RefreshList(int index){loading=true;items.Items.Clear();items.Items.Add("新建文件");foreach(var item in settings.Commands)items.Items.Add(item);selected=-1;loading=false;items.SelectedIndex=index;}
        void SelectItem(){
            if(loading)return;int next=items.SelectedIndex;StoreCurrent();selected=next;if(selected<0)return;
            command.Enabled=selected>0;command.Text=selected==0?"新建文件初始框架":settings.Commands[selected-1].Command;
            code.Text=selected==0?settings.Initial:settings.Commands[selected-1].Code;error.Text="";
        }
        void AddCommand(){
            StoreCurrent();int n=1;while(settings.Commands.Any(item=>item.Command=="/template"+n))n++;
            settings.Commands.Add(new CodeTemplate{Command="/template"+n,Code="${cursor}"});RefreshList(settings.Commands.Count);command.Focus();command.SelectAll();
        }
        void RemoveCommand(){if(selected<=0)return;int at=selected;settings.Commands.RemoveAt(at-1);RefreshList(Math.Min(at,settings.Commands.Count));}
        void ResetSelected(){
            if(selected<0)return;var defaults=TemplateSettings.Defaults();
            if(selected==0)code.Text=defaults.Initial;
            else{var item=defaults.Commands.FirstOrDefault(x=>x.Command==command.Text.Trim());code.Text=item==null?"${cursor}":item.Code;}
        }
        void SaveChanges(){try{StoreCurrent();settings.Save();DialogResult=DialogResult.OK;Close();}catch(Exception ex){error.Text=ex.Message;}}
    }
}
