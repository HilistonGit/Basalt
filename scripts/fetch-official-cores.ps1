param([string]$OutputDirectory='build/cores-current')
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$cache=Join-Path $root 'build/official-cores'
New-Item -ItemType Directory -Force $cache | Out-Null
$entries=Get-Content (Join-Path $PSScriptRoot 'legacy-cores.lock.json') -Raw | ConvertFrom-Json
$wintunArchive=Join-Path $cache 'wintun-0.14.1.zip'
if(!(Test-Path $wintunArchive)) { Invoke-WebRequest 'https://www.wintun.net/builds/wintun-0.14.1.zip' -OutFile $wintunArchive }
if((Get-FileHash $wintunArchive -Algorithm SHA256).Hash.ToLowerInvariant() -ne '07c256185d6ee3652e09fa55c0b673e2624b565e02c4b9091c79ca7d2f24ef51') { throw 'Wintun SHA256 mismatch.' }
Expand-Archive $wintunArchive (Join-Path $cache 'driver') -Force
foreach($entry in $entries) {
    $archive=Join-Path $cache $entry.archive
    if(!(Test-Path $archive)) { Invoke-WebRequest $entry.url -OutFile $archive }
    if((Get-FileHash $archive -Algorithm SHA256).Hash.ToLowerInvariant() -ne $entry.sha256) { throw "Archive SHA256 mismatch: $($entry.archive)" }
    $extract=Join-Path $cache ($entry.archive+'.extracted')
    Expand-Archive $archive $extract -Force
    $dest=Join-Path $root "$OutputDirectory/windows-legacy-$($entry.architecture)"
    New-Item -ItemType Directory -Force $dest | Out-Null
    $binary=@(Get-ChildItem $extract -Recurse -Filter ($entry.core+'.exe'))
    if($binary.Count -ne 1) { throw 'Expected one core executable.' }
    Copy-Item $binary[0].FullName $dest -Force
    if($entry.core -eq 'xray') { foreach($name in @('geoip.dat','geosite.dat')) { Copy-Item (Join-Path $binary[0].DirectoryName $name) $dest -Force } }
    foreach($license in Get-ChildItem $extract -Recurse -File | Where-Object Name -Match '^(LICENSE|COPYING)') { Copy-Item $license.FullName (Join-Path $dest ($entry.core+'-'+$license.Name)) -Force }
}
foreach($arch in @('amd64','386')) {
    $dest=Join-Path $root "$OutputDirectory/windows-legacy-$arch"
    $driverArch=if($arch -eq '386'){'x86'}else{'amd64'}
    Copy-Item (Join-Path $cache "driver/wintun/bin/$driverArch/wintun.dll") $dest -Force
    Copy-Item (Join-Path $cache 'driver/wintun/LICENSE.txt') (Join-Path $dest 'wintun-LICENSE.txt') -Force
    $files=@{}; foreach($file in @('xray.exe','sing-box.exe','wintun.dll','geoip.dat','geosite.dat')) { $files[$file]=(Get-FileHash (Join-Path $dest $file) -Algorithm SHA256).Hash.ToLowerInvariant() }
    @{channel='windows-legacy';architecture=$arch;distribution='official-upstream';xrayVersion='26.10.21';singBoxVersion='1.14.4';sources=@($entries | Where-Object architecture -eq $arch);files=$files} | ConvertTo-Json -Depth 6 | Set-Content (Join-Path $dest 'manifest.json') -Encoding utf8
}
