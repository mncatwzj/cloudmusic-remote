$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
& (Join-Path $PSScriptRoot 'Build.ps1')
& (Join-Path $PSScriptRoot 'Test.ps1')
$artifacts = Join-Path $root 'artifacts'
New-Item -ItemType Directory -Force $artifacts | Out-Null
$package = Join-Path $artifacts 'CloudMusicRemote-v1.0.0-win-x64.zip'
$files = @('CloudMusicRemote.exe','Allow-Phone.ps1','LICENSE','README.md','AI_DISCLOSURE.md','CHANGELOG.md','SECURITY.md','CONTRIBUTING.md') | ForEach-Object { Get-Item (Join-Path (Join-Path $root 'dist') $_) }
Compress-Archive -LiteralPath $files.FullName -DestinationPath $package -Force
$hash = (Get-FileHash -LiteralPath $package -Algorithm SHA256).Hash.ToLowerInvariant()
Set-Content -LiteralPath (Join-Path $artifacts 'SHA256SUMS.txt') -Encoding ASCII -Value ($hash + '  ' + (Split-Path -Leaf $package))
Write-Host ('Package: ' + $package)
