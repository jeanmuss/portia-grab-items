param([Parameter(Mandatory=$true)][string]$ReferenceDirectory)
$ErrorActionPreference = 'Stop'
$refsPath = (Resolve-Path -LiteralPath $ReferenceDirectory).Path
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
foreach ($name in 'mscorlib.dll','System.dll','System.Core.dll','Assembly-CSharp.dll','BepInEx.dll','0Harmony.dll','UnityEngine.CoreModule.dll','UnityEngine.IMGUIModule.dll') {
    if (!(Test-Path -LiteralPath (Join-Path $refsPath $name))) { throw "Missing reference: $name" }
}
$coreVersion = [Reflection.AssemblyName]::GetAssemblyName((Join-Path $refsPath 'mscorlib.dll')).Version
if ($coreVersion.Major -ne 2) { throw 'Use the game CLR 2.0 mscorlib.dll, not .NET 4.' }
$systemCoreVersion = [Reflection.AssemblyName]::GetAssemblyName((Join-Path $refsPath 'System.Core.dll')).Version
if ($systemCoreVersion.ToString() -ne '3.5.0.0') { throw 'Use the game System.Core 3.5.0.0 reference.' }
$source = Join-Path $PSScriptRoot 'src\PortiaGrabItem.cs'
$sourceText = Get-Content -LiteralPath $source -Raw
$versionMatch = [regex]::Match($sourceText, 'BepInPlugin\("local\.portia\.grabitems", "Portia Grab Items", "(\d+\.\d+\.\d+)"\)')
if (!$versionMatch.Success) { throw 'Cannot read the plugin version.' }
$version = $versionMatch.Groups[1].Value
$dist = Join-Path $PSScriptRoot 'dist'
$package = Join-Path $dist ('PortiaGrabItems-' + $version + '-BepInEx')
$plugins = Join-Path $package 'BepInEx\plugins'
New-Item -ItemType Directory -Path $plugins -Force | Out-Null
$refs = Get-ChildItem -LiteralPath $refsPath -Filter '*.dll' | ForEach-Object { '/reference:' + $_.FullName }
& $compiler /noconfig /nologo /nostdlib+ /target:library /optimize+ ('/out:' + (Join-Path $plugins 'PortiaGrabItem.dll')) $refs $source
if ($LASTEXITCODE -ne 0) { throw 'Compilation failed; no release ZIP was created.' }
foreach ($name in 'PortiaGrabItem.names.tsv','PortiaGrabItem.recipes.tsv') {
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot ('data\' + $name)) -Destination (Join-Path $plugins $name) -Force
}
foreach ($document in 'README.md','README.zh-CN.md','BUILDING.md','BUILDING.zh-CN.md','CHANGELOG.md','CHANGELOG.zh-CN.md') {
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot $document) -Destination $package -Force
}
$zip = Join-Path $dist ('PortiaGrabItems-' + $version + '-BepInEx.zip')
Compress-Archive -Path (Join-Path $package '*') -DestinationPath $zip -Force
Get-FileHash -LiteralPath $zip -Algorithm SHA256 | Format-List
