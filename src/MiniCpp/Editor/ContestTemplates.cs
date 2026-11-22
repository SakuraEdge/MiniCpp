using System;

namespace MiniCpp {
    internal static class ContestTemplates {
        internal static bool TryExpand(string command,string newline,out string code,out int caret) {
            code=null;caret=0;if(!command.StartsWith("/",StringComparison.Ordinal))return false;
            foreach(var item in TemplateSettings.Load().Commands)if(item.Command==command){code=TemplateSettings.Expand(item.Code,newline,out caret);return true;}
            return false;
        }
    }
}
