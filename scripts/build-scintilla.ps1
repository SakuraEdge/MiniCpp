param([Parameter(Mandatory=$true)][string]$Cache,[Parameter(Mandatory=$true)][string]$OutDir,[Parameter(Mandatory=$true)][string]$Csc)
$ErrorActionPreference='Stop'
$commit='2e5b28e0f5dff8c7c9de0eecd769aaf2e0d737bd' # upstream v3.6.3
$sha='DF52B0102371325EA7A1A0DE64354B5C5A04F61726E670CBB7105F2512870358'
$pkg=Join-Path $Cache 'scintillanet-v3.6.3-source.zip'
if (!(Test-Path -LiteralPath $pkg)) {
    $download="$pkg.$([Guid]::NewGuid()).tmp"
    try {
        & curl.exe --fail --location --retry 2 --silent --show-error --output $download "https://codeload.github.com/jacobslusser/ScintillaNET/zip/$commit"
        if ($LASTEXITCODE -ne 0 -or (Get-FileHash -LiteralPath $download -Algorithm SHA256).Hash -ne $sha) { throw 'Could not restore verified ScintillaNET sources.' }
        Move-Item -LiteralPath $download -Destination $pkg
    } finally { if(Test-Path -LiteralPath $download){Remove-Item -LiteralPath $download} }
}
if ((Get-FileHash -LiteralPath $pkg -Algorithm SHA256).Hash -ne $sha) { throw 'ScintillaNET source checksum mismatch.' }
$work=[IO.Directory]::CreateDirectory((Join-Path $Cache 'scintillanet-unicode')).FullName
$sources=@();$resources=@()
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip=[IO.Compression.ZipFile]::OpenRead($pkg)
try {
    foreach ($entry in $zip.Entries) {
        if ($entry.FullName -notmatch "^ScintillaNET-$commit/src/ScintillaNET/(?<name>(?:Properties/)?\w+\.cs|x(?:64|86)/SciLexer\.dll\.gz)$") { continue }
        $name=$Matches['name'];$path=Join-Path $work $name
        [void][IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($path))
        [IO.Compression.ZipFileExtensions]::ExtractToFile($entry,$path,$true)
        if($name.EndsWith('.cs')) { $sources+=$path } else { $resources+="/resource:$path,ScintillaNET.$($name.Replace('/','.'))" }
    }
} finally { $zip.Dispose() }

# Scintilla moves by scalars; WinForms selections and strings use UTF-16 units.
# Patch the shared conversion, so selection, search, indicators and typing all agree.
$file=Join-Path $work 'LineCollection.cs'
$source=[IO.File]::ReadAllText($file)
$old='bytePos = scintilla.DirectMessage(NativeMethods.SCI_POSITIONRELATIVE, new IntPtr(bytePos), new IntPtr(1)).ToInt32();'
$new=@'
var next = scintilla.DirectMessage(NativeMethods.SCI_POSITIONRELATIVE, new IntPtr(bytePos), new IntPtr(1)).ToInt32();
                if (next - bytePos == 4 && scintilla.Encoding.CodePage == 65001)
                {
                    if (pos == 1) break; // An index inside a surrogate pair snaps to its start.
                    pos--;
                }
                bytePos = next;
'@
if ($source.IndexOf($old) -lt 0 -or $source.IndexOf($old) -ne $source.LastIndexOf($old)) { throw 'Unexpected upstream position conversion.' }
[IO.File]::WriteAllText($file,$source.Replace($old,$new))
# The inbox Framework compiler supports C# 5; upstream uses three C# 6 property getters.
$reader=Join-Path $work 'ScintillaReader.cs'
[IO.File]::WriteAllText($reader,([IO.File]::ReadAllText($reader) -creplace '(private int \w+) => ([^;\r\n]+);','$1 { get { return $2; } }'))
& $Csc '/nologo' '/langversion:5' '/codepage:65001' '/utf8output' '/unsafe' '/optimize+' '/target:library' '/r:System.dll' '/r:System.Core.dll' '/r:System.Drawing.dll' '/r:System.Windows.Forms.dll' "/out:$OutDir/ScintillaNET.dll" @resources @sources
if ($LASTEXITCODE -ne 0) { throw 'Could not build Unicode-corrected ScintillaNET.' }
