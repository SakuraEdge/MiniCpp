[CmdletBinding()]
param([string]$AppDir)
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if (!$AppDir) { $AppDir = Join-Path $root 'dist/MiniCpp' }
$out = [IO.Directory]::CreateDirectory($AppDir).FullName
$target = Join-Path $out 'w64devkit'
if (Test-Path -LiteralPath $target) {
    if ((Test-Path -LiteralPath (Join-Path $target 'bin/g++.exe')) -and (Test-Path -LiteralPath (Join-Path $target 'bin/gdb.exe')) -and ((Get-Content -Raw -LiteralPath (Join-Path $target 'VERSION.txt')).Trim() -eq '2.11.0')) { Write-Output 'w64devkit 2.11.0 is already present.'; return }
    throw 'A different or incomplete toolchain is present. Move it aside before installing.'
}
$cache = [IO.Directory]::CreateDirectory((Join-Path $root '.build-cache')).FullName
$file = Join-Path $cache 'w64devkit-x64-2.11.0.7z.exe'
if (!(Test-Path -LiteralPath $file)) {
    & curl.exe --fail --location --retry 2 --silent --show-error --output $file 'https://github.com/skeeto/w64devkit/releases/download/v2.11.0/w64devkit-x64-2.11.0.7z.exe'
    if ($LASTEXITCODE -ne 0) { throw 'Toolchain download failed.' }
}
if ((Get-FileHash -Algorithm SHA256 -LiteralPath $file).Hash -ne '54C2BB9962E8A30C8270E34C5EFC3D5FEE579F53087FA7D435B95FBBA2AF7B2B') { throw 'Toolchain checksum mismatch.' }
Push-Location -LiteralPath $out
try { $p = Start-Process -FilePath $file -ArgumentList @('-y',('"-o{0}"' -f $out)) -WorkingDirectory $out -WindowStyle Hidden -Wait -PassThru; if ($p.ExitCode -ne 0) { throw "Extraction failed: $($p.ExitCode)" } } finally { Pop-Location }
if (!(Test-Path -LiteralPath (Join-Path $target 'bin/g++.exe'))) { throw 'Expected compiler was not extracted.' }
Write-Output "Toolchain: $target"
