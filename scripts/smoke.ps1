param([string]$AppDir,[switch]$Feedback)
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$app = if ($AppDir) { [IO.Path]::GetFullPath($AppDir) } else { Join-Path $root 'dist/MiniCpp' }
$run = [IO.Directory]::CreateDirectory((Join-Path $root ('.build-cache/smoke-' + [Guid]::NewGuid().ToString('N')))).FullName
$harness = Join-Path $app ('Smoke-' + [Guid]::NewGuid().ToString('N') + '.exe')
$csc = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
try {
    $src = if ($Feedback) { 'FeedbackSmoke.cs' } else { 'Smoke.cs' }
    & $csc '/nologo' '/codepage:65001' '/platform:x64' '/target:exe' '/r:System.Core.dll' '/r:System.Xml.Linq.dll' '/r:System.Windows.Forms.dll' '/r:System.Drawing.dll' "/r:$app/ScintillaNET.dll" "/out:$harness" (Join-Path $PSScriptRoot $src)
    if ($LASTEXITCODE -ne 0) { throw 'Could not build smoke harness.' }
    Copy-Item -LiteralPath (Join-Path $app 'MiniCpp.exe.config') -Destination "$harness.config"
    & $harness $run
    if ($LASTEXITCODE -ne 0) { throw "Smoke failed. Files: $run" }
} finally { foreach ($generated in $harness,"$harness.config") { if (Test-Path -LiteralPath $generated) { Remove-Item -LiteralPath $generated -Force } } }
