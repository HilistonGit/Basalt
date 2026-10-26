param([Parameter(Mandatory=$true)][string]$Xray, [Parameter(Mandatory=$true)][string]$SingBox, [string]$BuildDirectory='build')
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
$xrayFull = (Resolve-Path $Xray).Path
$singBoxFull = (Resolve-Path $SingBox).Path
Push-Location $root
try {
    & $compiler /nologo /target:exe /platform:x86 "/out:$BuildDirectory\Basalt.IntegrationTests.exe" "/reference:$BuildDirectory\Basalt.Core.dll" "/reference:$BuildDirectory\Basalt.exe" tests\IntegrationTests.cs tests\FormatFixtures.cs
    if ($LASTEXITCODE -ne 0) { throw 'Compilation failed.' }
    & (Join-Path $root "$BuildDirectory\Basalt.IntegrationTests.exe") $xrayFull $singBoxFull
    if ($LASTEXITCODE -ne 0) { throw 'Integration tests failed.' }
    & $compiler /nologo /target:exe /platform:x86 "/out:$BuildDirectory\RoutingRuntimeTests.exe" "/reference:$BuildDirectory\Basalt.Core.dll" "/reference:$BuildDirectory\Basalt.exe" tests\RoutingRuntimeTests.cs tests\FormatFixtures.cs
    if ($LASTEXITCODE -ne 0) { throw 'Routing tests compilation failed.' }
    & (Join-Path $root "$BuildDirectory\RoutingRuntimeTests.exe") $xrayFull $singBoxFull
    if ($LASTEXITCODE -ne 0) { throw 'Routing runtime tests failed.' }
} finally { Pop-Location }
