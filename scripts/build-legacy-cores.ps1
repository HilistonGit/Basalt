param([ValidateSet('amd64','386','all')][string]$Architecture='all', [switch]$Historical)
if (!$Historical) { & (Join-Path $PSScriptRoot 'fetch-official-cores.ps1'); exit }
# Historical reproduction only; shipping builds use pinned official releases.
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$workspace=Join-Path $root 'build\legacy'
$go=Join-Path $workspace 'toolchain\go\bin\go.exe'
$expected='2483b14ce111fa89ac671cdb00073c5b19670198be1334f491a8f3fe36affeb0'
if (!(Test-Path $go)) { throw 'Run scripts/fetch-legacy-sources.ps1 first.' }
if ((Get-FileHash (Join-Path $workspace 'go.zip') -Algorithm SHA256).Hash.ToLowerInvariant() -ne $expected) { throw 'Legacy toolchain hash mismatch.' }
$old=@{}; foreach($key in @('GOROOT','GOPATH','GOCACHE','GOTOOLCHAIN','GOOS','GOARCH','CGO_ENABLED')) { $old[$key]=[Environment]::GetEnvironmentVariable($key) }
try {
    $env:GOROOT=Join-Path $workspace 'toolchain\go'; $env:GOPATH=Join-Path $workspace 'gopath'; $env:GOCACHE=Join-Path $workspace 'cache'
    $env:GOTOOLCHAIN='local'; $env:GOOS='windows'; $env:CGO_ENABLED='0'
    $architectures=if($Architecture -eq 'all'){@('amd64','386')}else{@($Architecture)}
    foreach($arch in $architectures) {
        $env:GOARCH=$arch; $dest=Join-Path $root "build\cores\windows-legacy-$arch"; New-Item -ItemType Directory -Force $dest | Out-Null
        foreach($core in @(@{Folder='sing-box';Commit='54ed58499d7063136ed52dabf87d179d252425d0';Target='./cmd/sing-box';Exe='sing-box.exe';Tags='with_quic,with_utls,with_clash_api';Flags='-s -w -X github.com/sagernet/sing-box/constant.Version=1.12.12-basalt-legacy'},@{Folder='xray';Commit='b69a376aa1b61ad94c9c05b2c2cd263567b8cc23';Target='./main';Exe='xray.exe';Tags='';Flags='-s -w'})) {
            Push-Location (Join-Path $workspace $core.Folder)
            try {
                if ((git rev-parse HEAD) -ne $core.Commit) { throw 'Source revision mismatch.' }
                if (git status --porcelain --untracked-files=no) { throw 'Core source checkout is modified.' }
                & $go build -trimpath -buildvcs=true -tags $core.Tags -ldflags $core.Flags -o (Join-Path $dest $core.Exe) $core.Target
                if($LASTEXITCODE -ne 0) { throw "Build failed: $($core.Folder) $arch" }
                Copy-Item LICENSE (Join-Path $dest ($core.Folder+'-LICENSE')) -Force
            } finally { Pop-Location }
        }
        $driverArch=if($arch -eq '386'){'x86'}else{$arch}
        Copy-Item (Join-Path $workspace "wintun\wintun\bin\$driverArch\wintun.dll") $dest -Force
        Copy-Item (Join-Path $workspace 'wintun\wintun\LICENSE.txt') (Join-Path $dest 'wintun-LICENSE.txt') -Force
        $files=@{}; foreach($file in @('xray.exe','sing-box.exe','wintun.dll')) { $files[$file]=(Get-FileHash (Join-Path $dest $file) -Algorithm SHA256).Hash.ToLowerInvariant() }
        @{channel='windows-legacy';architecture=$arch;toolchain='MetaCubeX go1.26.10';toolchainSha256=$expected;xrayCommit='b69a376aa1b61ad94c9c05b2c2cd263567b8cc23';singBoxCommit='54ed58499d7063136ed52dabf87d179d252425d0';files=$files} | ConvertTo-Json -Depth 4 | Set-Content (Join-Path $dest 'manifest.json') -Encoding utf8
    }
} finally { foreach($key in $old.Keys) { [Environment]::SetEnvironmentVariable($key,$old[$key]) } }
