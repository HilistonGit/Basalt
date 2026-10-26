$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$workspace=Join-Path $root 'build\legacy'
New-Item -ItemType Directory -Force $workspace | Out-Null
$archive=Join-Path $workspace 'go.zip'
if (!(Test-Path $archive)) { Invoke-WebRequest 'https://github.com/MetaCubeX/go/releases/download/build/go1.26.windows-amd64.zip' -OutFile $archive }
if ((Get-FileHash $archive -Algorithm SHA256).Hash.ToLowerInvariant() -ne '2483b14ce111fa89ac671cdb00073c5b19670198be1334f491a8f3fe36affeb0') { throw 'Pinned Go hash mismatch. The upstream rolling asset may have changed; do not silently accept it.' }
if (!(Test-Path (Join-Path $workspace 'toolchain\go\bin\go.exe'))) { Expand-Archive $archive (Join-Path $workspace 'toolchain') }
foreach($repo in @(@{Name='sing-box';Url='https://github.com/SagerNet/sing-box.git';Tag='v1.12.12'},@{Name='xray';Url='https://github.com/XTLS/Xray-core.git';Tag='v25.10.15'})) {
    $dest=Join-Path $workspace $repo.Name
    if (!(Test-Path $dest)) { git clone --depth 1 --branch $repo.Tag $repo.Url $dest; if($LASTEXITCODE -ne 0){throw 'Clone failed.'} }
}
$wintun=Join-Path $workspace 'wintun.zip'
if (!(Test-Path $wintun)) { Invoke-WebRequest 'https://www.wintun.net/builds/wintun-0.14.1.zip' -OutFile $wintun }
if ((Get-FileHash $wintun -Algorithm SHA256).Hash.ToLowerInvariant() -ne '07c256185d6ee3652e09fa55c0b673e2624b565e02c4b9091c79ca7d2f24ef51') { throw 'Wintun hash mismatch.' }
if (!(Test-Path (Join-Path $workspace 'wintun'))) { Expand-Archive $wintun (Join-Path $workspace 'wintun') }
