$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$validation = Join-Path $root 'build\validation'
New-Item -ItemType Directory -Force $validation | Out-Null
$package = Join-Path $validation 'net40.zip'
Invoke-WebRequest 'https://api.nuget.org/v3-flatcontainer/microsoft.netframework.referenceassemblies.net40/1.0.3/microsoft.netframework.referenceassemblies.net40.1.0.3.nupkg' -OutFile $package
Expand-Archive $package (Join-Path $validation 'net40') -Force
Write-Host 'Official .NET Framework 4.0 references are ready. Run scripts/build.ps1.'
