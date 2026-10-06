param([ValidateRange(17663,17672)][int]$Port = 17663)
$ErrorActionPreference = 'Stop'
$program = Join-Path $PSScriptRoot 'CloudMusicRemote.exe'
$name = 'CloudMusicRemote-LAN-' + $Port
if (-not (Test-Path -LiteralPath $program)) { throw 'CloudMusicRemote.exe missing' }
Get-NetFirewallRule -Name $name -ErrorAction SilentlyContinue | Remove-NetFirewallRule
New-NetFirewallRule -Name $name -DisplayName 'CloudMusicRemote - phone on local subnet' -Direction Inbound -Action Allow -Protocol TCP -LocalPort $Port -RemoteAddress LocalSubnet -Program $program -Profile Private,Public | Out-Null
