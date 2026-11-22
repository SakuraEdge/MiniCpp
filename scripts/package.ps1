[CmdletBinding()]
param([string]$AppDir)
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$app = if ($AppDir) { [IO.Path]::GetFullPath($AppDir) } else { Join-Path $root 'dist/MiniCpp' }
foreach ($item in 'MiniCpp.exe','MiniCpp.exe.config','MiniCpp.Console.exe','ScintillaNET.dll','w64devkit/bin/g++.exe','w64devkit/bin/gdb.exe','assets/mini-cpp.ico','assets/cpp-file.ico','LICENSE','LICENSES/TOOLCHAIN.txt') {
    if (!(Test-Path -LiteralPath (Join-Path $app $item))) { throw "Missing $item; build and install the toolchain first." }
}
$out = [IO.Directory]::CreateDirectory((Join-Path $root 'artifacts')).FullName
$info = [Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path $app 'MiniCpp.exe'))
$version = '{0}.{1}.{2}' -f $info.FileMajorPart,$info.FileMinorPart,$info.FileBuildPart
switch -Exact ($info.ProductVersion) {
    $version { $channel='Rev'; break }
    "$version-dev" { $channel='Dev'; break }
    default { throw "Unsupported product version: $($info.ProductVersion)" }
}
$path = Join-Path $out "MiniCpp-$channel-v$version-win-x64-Portable.zip"
$temp = Join-Path $out ([Guid]::NewGuid().ToString('N') + '.zip')
Add-Type -AssemblyName System.IO.Compression.FileSystem
try {
    [IO.Compression.ZipFile]::CreateFromDirectory($app,$temp,[IO.Compression.CompressionLevel]::Optimal,$true)
    $archive = [IO.Compression.ZipFile]::Open($temp,[IO.Compression.ZipArchiveMode]::Update)
    try {
        $marker = (Split-Path -Leaf $app) + '/portable.mode'
        if (!$archive.GetEntry($marker)) { $null = $archive.CreateEntry($marker) }
        # Supply external manifests for compiler helpers that do not opt in to UTF-8 paths.
        $helpers = @(Get-ChildItem -LiteralPath (Join-Path $app 'w64devkit/libexec') -Filter 'collect2.exe' -File -Recurse)
        $helpers += @(foreach ($name in 'ld.exe','as.exe','x86_64-w64-mingw32-ld.exe','x86_64-w64-mingw32-as.exe') {
            Get-Item -LiteralPath (Join-Path $app "w64devkit/bin/$name")
        })
        foreach ($helper in $helpers) {
            $entry = (Split-Path -Leaf $app) + '/' + $helper.FullName.Substring($app.TrimEnd('\').Length+1).Replace('\','/') + '.manifest'
            if (!$archive.GetEntry($entry)) {
                $null = [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive,(Join-Path $PSScriptRoot 'toolchain-utf8.manifest'),$entry)
            }
        }
    } finally { $archive.Dispose() }
    if ([IO.File]::Exists($path)) { [IO.File]::Replace($temp,$path,$null) } else { [IO.File]::Move($temp,$path) }
} finally { if (Test-Path -LiteralPath $temp) { Remove-Item -LiteralPath $temp -Force } }
Get-FileHash -Algorithm SHA256 -LiteralPath $path
