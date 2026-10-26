param([switch]$Run, [string]$BuildDirectory='build')
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
Push-Location $root
try {
    $compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
    & $compiler /nologo /target:exe /platform:x86 "/out:$BuildDirectory\Basalt.TunSmokeTests.exe" "/reference:$BuildDirectory\Basalt.Core.dll" "/reference:$BuildDirectory\Basalt.exe" tests\TunSmokeTests.cs
    if($LASTEXITCODE -ne 0){throw 'Compilation failed.'}
    if($Run) {
        # Explicit opt-in: changes routes temporarily and triggers Windows UAC consent.
        $process=Start-Process (Join-Path $root "$BuildDirectory\Basalt.TunSmokeTests.exe") -Verb RunAs -WindowStyle Hidden -Wait -PassThru
        Get-Content (Join-Path $root "$BuildDirectory\tun-smoke-result.txt")
        if($process.ExitCode -ne 0){throw 'TUN smoke test failed. Inspect build/tun-smoke-result.txt.'}
    } else { Write-Host 'Test compiled. Use -Run to request UAC and perform the network test.' }
} finally { Pop-Location }
