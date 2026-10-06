$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
Push-Location $root
try {
    if (-not (Test-Path dist\CloudMusicRemote.exe)) { throw 'Run scripts/Build.ps1 first.' }
    & $compiler /nologo /codepage:65001 /target:exe /platform:x64 /out:dist\RemoteTests.exe /r:dist\CloudMusicRemote.exe /r:System.Web.Extensions.dll tests\RemoteTests.cs
    if ($LASTEXITCODE -ne 0) { throw 'Test compilation failed.' }
    & .\dist\RemoteTests.exe
    if ($LASTEXITCODE -ne 0) { throw 'Tests failed.' }
} finally { Pop-Location }
