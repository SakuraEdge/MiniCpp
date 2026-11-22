[CmdletBinding()]
param([string]$AppDir)
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if (!$AppDir) { $AppDir=Join-Path $root 'dist/MiniCpp' }
$app=(Resolve-Path -LiteralPath $AppDir).Path.TrimEnd('\')
$version=[Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path $app 'MiniCpp.exe')).ProductVersion
if ($version -notmatch '^\d+\.\d+\.\d+(-dev)?$') { throw 'Invalid application version' }
$channel=if($version.EndsWith('-dev')){'Dev'}else{'Rev'}
$out=[IO.Directory]::CreateDirectory((Join-Path $root 'artifacts')).FullName
$name="MiniCpp-$channel-v$($version.Replace('-dev',''))-win-x64-Update.zip"
$archivePath=Join-Path $out $name
$files=@(foreach($name in 'MiniCpp.exe','MiniCpp.exe.config','MiniCpp.Console.exe','MiniCpp.Updater.exe','ScintillaNET.dll','README.md','LICENSE') { Get-Item -LiteralPath (Join-Path $app $name) })
$files+=@(Get-ChildItem -LiteralPath (Join-Path $app 'assets') -File)
$files+=@(Get-ChildItem -LiteralPath (Join-Path $app 'LICENSES') -File)
$xml=[Xml.XmlDocument]::new()
$document=$xml.CreateElement('MiniCppUpdate');$document.SetAttribute('version',$version);$null=$xml.AppendChild($document)
foreach($file in $files){
    $relative=$file.FullName.Substring($app.Length+1).Replace('\','/')
    $entry=$xml.CreateElement('File');$entry.SetAttribute('path',$relative)
    $entry.SetAttribute('sha256',(Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant())
    $null=$document.AppendChild($entry)
}
$manifest=Join-Path $app 'update-manifest.xml'
$xml.Save($manifest)
$temp=Join-Path $out ([Guid]::NewGuid().ToString('N')+'.zip')
Add-Type -AssemblyName System.IO.Compression.FileSystem
try {
    $archive=[IO.Compression.ZipFile]::Open($temp,[IO.Compression.ZipArchiveMode]::Create)
    try {
        foreach($file in ($files+@(Get-Item -LiteralPath $manifest))){
            $relative=$file.FullName.Substring($app.Length+1).Replace('\','/')
            $null=[IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive,$file.FullName,$relative,[IO.Compression.CompressionLevel]::Optimal)
        }
    } finally { $archive.Dispose() }
    if(Test-Path -LiteralPath $archivePath){[IO.File]::Replace($temp,$archivePath,$null)}else{[IO.File]::Move($temp,$archivePath)}
} finally { if(Test-Path -LiteralPath $temp){Remove-Item -LiteralPath $temp -Force} }
$descriptor=@{
    version=$version
    file=[IO.Path]::GetFileName($archivePath)
    sha256=(Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash.ToLowerInvariant()
    size=(Get-Item -LiteralPath $archivePath).Length
    toolchain=(Get-Content -Raw -LiteralPath (Join-Path $app 'w64devkit/VERSION.txt')).Trim()
}
[IO.File]::WriteAllText((Join-Path $out 'update.json'),($descriptor|ConvertTo-Json),[Text.UTF8Encoding]::new($false))
Get-FileHash -LiteralPath $archivePath -Algorithm SHA256
