using System;

namespace MiniCpp {
    internal static class ContestTemplates {
        const string Header = "#include <bits/stdc++.h>\nusing namespace std;\n";
        // The marker is never inserted into the document; it sets the final caret.
        const char Caret = '|';

        internal static bool TryExpand(string command,string newline,out string code,out int caret) {
            string body;
            switch (command) {
                case "/main": body="int main() {\n    |return 0;\n}"; break;
                case "/fremain": body="int main() {\n    freopen(\".in\",\"r\",stdin);\n    freopen(\".out\",\"w\",stdout);\n    |return 0;\n}"; break;
                case "/solve": body="\nvoid solve() {\n    |\n}\n\nint main() {\n    solve();\n    return 0;\n}"; break;
                default: code=null; caret=0; return false;
            }
            string template=(Header+body).Replace("\n",newline);
            caret=template.IndexOf(Caret);
            code=template.Remove(caret,1);
            return true;
        }
    }
}
