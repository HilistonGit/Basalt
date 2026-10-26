param([string]$BuildDirectory='build',[string]$Gh='gh')
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
Push-Location $root
try {
 $version=(Get-Content VERSION -Raw).Trim()
 if($version -notmatch '^\d+\.\d+\.\d+$'){throw 'Invalid VERSION'}
 $actual=[Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path $root "$BuildDirectory/Basalt.exe")).FileVersion
 if($actual -ne "$version.0"){throw 'Built version differs from VERSION'}
 ./scripts/package.ps1 -BuildDirectory $BuildDirectory
 ./scripts/package.ps1 -BuildDirectory $BuildDirectory -Architecture x86
 $assets=@("dist/Basalt-$version-test.zip","dist/Basalt-$version-test-x86.zip",'build/release-sources/sing-box-1.14.4-source-with-vendor.tar.gz','build/release-sources/Xray-core-26.10.21-source.tar.gz')
 foreach($file in $assets){if(!(Test-Path $file)){throw "Missing release asset: $file"}}
 $sums=foreach($file in $assets){((Get-FileHash $file -Algorithm SHA256).Hash.ToLowerInvariant())+'  '+[IO.Path]::GetFileName($file)}
 [IO.File]::WriteAllLines((Join-Path $root 'dist/SHA256SUMS.txt'),$sums,[Text.UTF8Encoding]::new($false))
 & $Gh release create "v$version" @assets dist/SHA256SUMS.txt --repo HilistonGit/Basalt --target main --title "Basalt $version" --notes-file docs/RELEASE-NOTES.md
 if($LASTEXITCODE -ne 0){throw 'GitHub release failed'}
} finally {Pop-Location}
