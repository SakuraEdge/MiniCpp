[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$app = Join-Path $root 'dist/MiniCpp'
foreach ($item in 'MiniCpp.exe','MiniCpp.Console.exe','ScintillaNET.dll','w64devkit/bin/g++.exe','w64devkit/bin/gdb.exe') {
    if (!(Test-Path -LiteralPath (Join-Path $app $item))) { throw "Missing $item; build and install the toolchain first." }
}
$out = [IO.Directory]::CreateDirectory((Join-Path $root 'artifacts')).FullName
$path = Join-Path $out 'MiniCpp-0.1.0-win-x64.zip'
$temp = Join-Path $out ([Guid]::NewGuid().ToString('N') + '.zip')
Add-Type -AssemblyName System.IO.Compression.FileSystem
try {
    [IO.Compression.ZipFile]::CreateFromDirectory($app,$temp,[IO.Compression.CompressionLevel]::Optimal,$true)
    if ([IO.File]::Exists($path)) { [IO.File]::Replace($temp,$path,$null) } else { [IO.File]::Move($temp,$path) }
} finally { if (Test-Path -LiteralPath $temp) { Remove-Item -LiteralPath $temp -Force } }
Get-FileHash -Algorithm SHA256 -LiteralPath $path
