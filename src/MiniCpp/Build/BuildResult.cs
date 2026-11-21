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
    internal sealed class BuildResult {
        public bool Success; public string Log, Executable; public List<Diagnostic> Diagnostics = new List<Diagnostic>();
    }
}
