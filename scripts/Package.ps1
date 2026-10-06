$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
& (Join-Path $PSScriptRoot 'Build.ps1')
& (Join-Path $PSScriptRoot 'Test.ps1')
$artifacts = Join-Path $root 'artifacts'
New-Item -ItemType Directory -Force $artifacts | Out-Null
$package = Join-Path $artifacts 'CloudMusicRemote-v0.1.0-win-x64.zip'
$files = Get-ChildItem (Join-Path $root 'dist') | Where-Object { $_.Name -ne 'RemoteTests.exe' }
Compress-Archive -LiteralPath $files.FullName -DestinationPath $package -Force
$hash = (Get-FileHash -LiteralPath $package -Algorithm SHA256).Hash.ToLowerInvariant()
Set-Content -LiteralPath (Join-Path $artifacts 'SHA256SUMS.txt') -Encoding ASCII -Value ($hash + '  ' + (Split-Path -Leaf $package))
Write-Host ('Package: ' + $package)
