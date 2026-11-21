using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace MiniCpp {
    internal sealed class Diagnostic {
        public string File, Severity, Message, Suggestion; public int Line, Column, HintLine, HintColumn;
    }
}
