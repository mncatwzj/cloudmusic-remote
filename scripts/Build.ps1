$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) { throw 'Windows x64 and .NET Framework are required.' }
$destination = Join-Path $root 'dist'
New-Item -ItemType Directory -Force $destination | Out-Null
Push-Location $root
try {
    & $compiler /nologo /codepage:65001 /target:winexe /platform:x64 /optimize+ /out:dist\CloudMusicRemote.exe /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Web.Extensions.dll /resource:src\remote.html,remote.html src\CloudMusicRemote.cs src\AssemblyInfo.cs
    if ($LASTEXITCODE -ne 0) { throw 'Compilation failed.' }
    Copy-Item scripts\Allow-Phone.ps1 $destination -Force
    Copy-Item LICENSE,README.md,AI_DISCLOSURE.md,CHANGELOG.md,SECURITY.md,CONTRIBUTING.md $destination -Force
    New-Item -ItemType Directory -Force (Join-Path $destination 'docs') | Out-Null
    Copy-Item docs\TESTING.md (Join-Path $destination 'docs') -Force
    Write-Host 'Build succeeded: dist/CloudMusicRemote.exe'
} finally { Pop-Location }
