[CmdletBinding()]
param([string]$AppDir,[string]$MakeNsis)
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if (!$AppDir){$AppDir=Join-Path $root 'dist/MiniCpp'}
$app=(Resolve-Path -LiteralPath $AppDir).Path.TrimEnd('\')
foreach ($file in 'MiniCpp.exe','MiniCpp.exe.config','MiniCpp.Console.exe','ScintillaNET.dll','w64devkit/bin/g++.exe','w64devkit/bin/gdb.exe','assets/cpp-file.ico','LICENSE'){
    if (!(Test-Path -LiteralPath (Join-Path $app $file) -PathType Leaf)){throw "Missing $file in staged distribution."}
}
if (!$MakeNsis){$cmd=Get-Command makensis.exe -ErrorAction SilentlyContinue;if($cmd){$MakeNsis=$cmd.Source}}
if (!$MakeNsis){$MakeNsis=Get-ChildItem -Path "${env:ProgramFiles(x86)}/NSIS/makensis.exe","$env:LOCALAPPDATA/electron-builder/Cache/nsis-*/*/makensis.exe" -File -ErrorAction SilentlyContinue | Select-Object -First 1 -ExpandProperty FullName}
if (!$MakeNsis){throw 'Install Unicode NSIS 3 or pass -MakeNsis <path>.'}
$out=[IO.Directory]::CreateDirectory((Join-Path $root 'artifacts')).FullName
$manifest=Join-Path ([IO.Directory]::CreateDirectory((Join-Path $root '.build-cache/installer')).FullName) 'uninstall-files.nsh'
$files=@(Get-ChildItem -LiteralPath $app -Recurse -File)
$dirs=@(Get-ChildItem -LiteralPath $app -Recurse -Directory | Sort-Object { $_.FullName.Length } -Descending)
# Only remove named package files. Leave source files and nonempty directories alone.
$lines=@(foreach($f in $files){'Delete "$INSTDIR\'+$f.FullName.Substring($app.Length+1).Replace('$','$$')+'"'};foreach($d in $dirs){'RMDir "$INSTDIR\'+$d.FullName.Substring($app.Length+1).Replace('$','$$')+'"'})
[IO.File]::WriteAllLines($manifest,[string[]]$lines,(New-Object Text.UTF8Encoding $true))
$info=[Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path $app 'MiniCpp.exe'))
$version='{0}.{1}.{2}' -f $info.FileMajorPart,$info.FileMinorPart,$info.FileBuildPart
switch -Exact ($info.ProductVersion) {
    $version { $channel='Rev'; break }
    "$version-dev" { $channel='Dev'; break }
    default { throw "Unsupported product version: $($info.ProductVersion)" }
}
$setup=Join-Path $out "MiniCpp-$channel-v$version-win-x64-Setup.exe"
& $MakeNsis '/V2' '/INPUTCHARSET' 'UTF8' "/DAPPVERSION=$version" "/DAPPCHANNEL=$channel" "/DPRODUCTVERSION=$($info.ProductVersion)" "/DAPPDIR=$app" "/DOUTPUT=$setup" "/DUNFILES=$manifest" (Join-Path $root 'installer/MiniCpp.nsi')
if ($LASTEXITCODE -ne 0){throw 'NSIS build failed.'}
Get-FileHash -Algorithm SHA256 -LiteralPath $setup
