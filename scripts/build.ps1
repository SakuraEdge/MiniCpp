[CmdletBinding()]
param([string]$AppDir)
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if (!$AppDir) { $AppDir = Join-Path $root 'dist/MiniCpp' }
$out = [IO.Directory]::CreateDirectory($AppDir).FullName
$assets = [IO.Directory]::CreateDirectory((Join-Path $out 'assets')).FullName
$cache = [IO.Directory]::CreateDirectory((Join-Path $root '.build-cache')).FullName
$csc = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
if (!(Test-Path -LiteralPath $csc)) { throw 'Install the Windows .NET Framework 4.8 runtime.' }
& (Join-Path $PSScriptRoot 'build-scintilla.ps1') -Cache $cache -OutDir $out -Csc $csc
$common = @('/nologo','/langversion:5','/codepage:65001','/utf8output','/optimize+','/platform:x64','/r:System.dll','/r:System.Core.dll','/r:System.Xml.dll','/r:System.Xml.Linq.dll')
$shared = @(Get-ChildItem -LiteralPath (Join-Path $root 'src/Shared') -Recurse -File -Filter '*.cs' | Sort-Object FullName | ForEach-Object FullName)
$gui = @(Get-ChildItem -LiteralPath (Join-Path $root 'src/MiniCpp') -Recurse -File -Filter '*.cs' | Sort-Object FullName | ForEach-Object FullName)
$console = @(Get-ChildItem -LiteralPath (Join-Path $root 'src/ConsoleHost') -Recurse -File -Filter '*.cs' | Sort-Object FullName | ForEach-Object FullName)
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
Copy-Item -LiteralPath (Join-Path $root 'LICENSE') -Destination (Join-Path $out 'LICENSE') -Force
Write-Output "Built: $out/MiniCpp.exe"
