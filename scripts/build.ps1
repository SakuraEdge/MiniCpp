[CmdletBinding()]
param([string]$AppDir)
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if (!$AppDir) { $AppDir = Join-Path $root 'dist/MiniCpp' }
$out = [IO.Directory]::CreateDirectory($AppDir).FullName
$assets = [IO.Directory]::CreateDirectory((Join-Path $out 'assets')).FullName
$cache = [IO.Directory]::CreateDirectory((Join-Path $root '.build-cache')).FullName
$pkg = Join-Path $cache 'scintilla-3.6.3.nupkg'
$sha = 'B75BC56170E36597177EB1D550C1AC056679099249DC7365734759E6C015FE08'
if (!(Test-Path -LiteralPath $pkg)) {
    & curl.exe --fail --location --retry 2 --silent --show-error --output $pkg 'https://api.nuget.org/v3-flatcontainer/jacobslusser.scintillanet/3.6.3/jacobslusser.scintillanet.3.6.3.nupkg'
    if ($LASTEXITCODE -ne 0) { throw 'Could not restore ScintillaNET.' }
}
if ((Get-FileHash -Algorithm SHA256 -LiteralPath $pkg).Hash -ne $sha) { throw 'ScintillaNET checksum mismatch.' }
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = [IO.Compression.ZipFile]::OpenRead($pkg)
try { [IO.Compression.ZipFileExtensions]::ExtractToFile($zip.GetEntry('lib/net40/ScintillaNET.dll'), (Join-Path $out 'ScintillaNET.dll'), $true) } finally { $zip.Dispose() }
$csc = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
if (!(Test-Path -LiteralPath $csc)) { throw 'Install the Windows .NET Framework 4.8 runtime.' }
$common = @('/nologo','/langversion:5','/codepage:65001','/utf8output','/optimize+','/platform:x64','/r:System.dll','/r:System.Core.dll','/r:System.Xml.dll','/r:System.Xml.Linq.dll')
$shared = @(Get-ChildItem -LiteralPath (Join-Path $root 'src/Shared') -Filter '*.cs' | ForEach-Object FullName)
$gui = @(Get-ChildItem -LiteralPath (Join-Path $root 'src/MiniCpp') -Filter '*.cs' | ForEach-Object FullName)
$console = @(Get-ChildItem -LiteralPath (Join-Path $root 'src/ConsoleHost') -Filter '*.cs' | ForEach-Object FullName)
& $csc @common '/target:exe' "/out:$out/MiniCpp.Console.exe" @shared @console
if ($LASTEXITCODE -ne 0) { throw 'Console host compilation failed.' }
& $csc @common '/target:winexe' '/r:System.Drawing.dll' '/r:System.Windows.Forms.dll' '/r:System.Runtime.Serialization.dll' '/r:System.Net.Http.dll' "/r:$out/ScintillaNET.dll" "/win32icon:$root/design/icons/mini-cpp.ico" "/win32manifest:$root/src/MiniCpp/app.manifest" "/out:$out/MiniCpp.exe" @shared @gui
if ($LASTEXITCODE -ne 0) { throw 'IDE compilation failed.' }
Get-ChildItem -LiteralPath (Join-Path $root 'src/Assets') -Filter '*.png' | ForEach-Object { Copy-Item -LiteralPath $_.FullName -Destination $assets -Force }
foreach ($name in 'mini-cpp.ico','cpp-file.ico','cpp-file-48.png') { Copy-Item -LiteralPath (Join-Path $root "design/icons/$name") -Destination $assets -Force }
Copy-Item -LiteralPath (Join-Path $root 'src/MiniCpp/MiniCpp.exe.config') -Destination $out -Force
$licenses = [IO.Directory]::CreateDirectory((Join-Path $out 'LICENSES')).FullName
Get-ChildItem -LiteralPath (Join-Path $root 'LICENSES') -File | ForEach-Object { Copy-Item -LiteralPath $_.FullName -Destination $licenses -Force }
Copy-Item -LiteralPath (Join-Path $root 'design/vendor/LUCIDE-LICENSE.txt') -Destination $licenses -Force
Copy-Item -LiteralPath (Join-Path $root 'README.md') -Destination (Join-Path $out 'README.md') -Force
Write-Output "Built: $out/MiniCpp.exe"
