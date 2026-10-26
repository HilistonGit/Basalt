param([string]$Go='go')
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$dest=Join-Path $root 'build/release-sources'
New-Item -ItemType Directory -Force $dest | Out-Null
$sing=Join-Path $dest 'sing-box'
if(!(Test-Path $sing)){git clone --depth 1 --branch v1.14.4 https://github.com/SagerNet/sing-box.git $sing;if($LASTEXITCODE -ne 0){throw 'Clone failed'}}
$tag=git -C $sing describe --tags --exact-match
if($tag -ne 'v1.14.4'){throw 'Unexpected sing-box source version'}
& $Go -C $sing mod vendor
if($LASTEXITCODE -ne 0){throw 'Dependency vendoring failed'}
& tar.exe -czf (Join-Path $dest 'sing-box-1.14.4-source-with-vendor.tar.gz') --exclude=.git -C $dest sing-box
if($LASTEXITCODE -ne 0){throw 'Source packaging failed'}
Invoke-WebRequest 'https://github.com/XTLS/Xray-core/archive/refs/tags/v26.10.21.tar.gz' -OutFile (Join-Path $dest 'Xray-core-26.10.21-source.tar.gz')
Get-ChildItem $dest -Filter *.tar.gz | Get-FileHash -Algorithm SHA256
