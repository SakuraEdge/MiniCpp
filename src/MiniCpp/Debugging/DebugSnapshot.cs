using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MiniCpp {
    internal sealed class DebugVariable { public string Name,Type,Value; }
    internal sealed class DebugSnapshot { public string File,Function; public int Line; public bool Sample; public List<DebugVariable> Variables=new List<DebugVariable>(); }
}
