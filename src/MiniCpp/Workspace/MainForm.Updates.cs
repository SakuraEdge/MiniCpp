using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace MiniCpp {
    internal sealed partial class MainForm {
        bool checkingUpdates;
        PreparedUpdate pendingUpdate;
        void CheckForUpdates(){
            if(checkingUpdates||closing)return;checkingUpdates=true;
            try{
                using(var dialog=new UpdateDialog()){
                    if(dialog.ShowDialog(this)!=DialogResult.OK||dialog.Prepared==null)return;
                    if(owner!=null){Error("请先结束编译、运行或调试任务，再进行更新。");return;}
                    foreach(var doc in docs.ToArray())if(doc.Edit.Modified&&!Save(doc,false,false))return;
                    pendingUpdate=dialog.Prepared;Close();pendingUpdate=null;
                }
            }finally{checkingUpdates=false;}
        }
        bool StartPendingUpdate(){
            if(pendingUpdate==null)return true;
            try{
                UpdateDownload.Launch(pendingUpdate,docs.Where(d=>d.FullPath!=null).Select(d=>d.FullPath).ToArray());return true;
            }catch(Exception ex){Error(ex.Message);return false;}
        }
    }
}
