# Third-party notices

Obelisk (MIT) is a design reference for dual-core routing. No source was copied.
Throne (GPL-3.0) was inspected as a legacy-build reference; no UI/source/assets
from Throne were copied. Basalt uses the patched MetaCubeX Go toolchain approach.

The portable distribution bundles independent executable processes:
- Xray-core 26.10.21, MPL-2.0: https://github.com/XTLS/Xray-core/releases/tag/v26.10.21
- sing-box 1.14.4, GPL-3.0-or-later: https://github.com/SagerNet/sing-box/releases/tag/v1.14.4
- Wintun 0.14.1, included LICENSE.txt: https://www.wintun.net/
- MetaCubeX patched Go toolchain: https://github.com/MetaCubeX/go

Licenses and binary hashes are included with each core architecture. Current binaries are official upstream Windows 7 legacy release assets;
their exact URLs and archive SHA256 are pinned in scripts/legacy-cores.lock.json. Historical source builds remain under build/legacy;
reproduction scripts are in scripts/. GitHub Releases provide the exact tagged Xray source and sing-box source with vendored Go dependencies next to the binary packages. Upstream build workflows and license files are retained in the source archives. Basalt's own code is GPL-3.0-only, copyright 2026 HilistonGit.

YamlDotNet 6.1.2, MIT, https://github.com/aaubry/YamlDotNet:
net35 binary is bundled for .NET Framework 4 compatibility. License is included
as YamlDotNet-LICENSE.txt. NuGet archive SHA-256:
D591A73285AB1C6075B16D23E99F300DE84902BB270D35C5FD134A6BC1220231.
Vendored DLL SHA-256:
4636B36DBECCDFA90FBE0ED04A75E1DC6019ED217B7109F5594F954C3E92158A.
