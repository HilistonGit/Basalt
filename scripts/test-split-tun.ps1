param([switch]$Run, [string]$BuildDirectory='build/release-0.6.4')
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
Push-Location $root
try {
 $compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
 & $compiler /nologo /platform:x86 "/out:$BuildDirectory\SplitTunRegression.exe" "/r:$BuildDirectory\Basalt.Core.dll" "/r:$BuildDirectory\Basalt.exe" tests\SplitTunRegression.cs
 if($LASTEXITCODE -ne 0){throw 'Compilation failed'}
 if($Run){
  # Uses saved Happ rules read-only; never connects to the user's VPN server.
  $active=[Net.NetworkInformation.NetworkInterface]::GetAllNetworkInterfaces() | Where-Object {$_.Name -like 'Basalt-*' -and $_.OperationalStatus -eq 'Up'}
  if($active){throw 'Disconnect the existing Basalt TUN before running this test.'}
  $process=Start-Process (Join-Path $root "$BuildDirectory\SplitTunRegression.exe") -Verb RunAs -WindowStyle Hidden -Wait -PassThru
  Get-Content (Join-Path $root "$BuildDirectory\split-tun-result.txt")
  if($process.ExitCode -ne 0){throw 'Split TUN regression failed'}
 }
} finally {Pop-Location}
