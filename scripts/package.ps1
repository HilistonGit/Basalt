param([ValidateSet('universal','x86')][string]$Architecture='universal', [string]$BuildDirectory='build', [string]$CoreDirectory='build/cores-current')
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$version=(Get-Content (Join-Path $root 'VERSION') -Raw).Trim()
if($version -notmatch '^\d+\.\d+\.\d+$'){throw 'Invalid VERSION'}
$suffix=if($Architecture -eq 'x86'){'-x86'}else{''}
$dest=Join-Path $root ("dist\Basalt-$version-test$suffix")
New-Item -ItemType Directory -Force $dest,(Join-Path $dest 'cores'),(Join-Path $dest 'docs/images') | Out-Null
foreach($file in @('Basalt.exe','YamlDotNet.dll','Basalt.Core.dll','Basalt.exe.config')) { Copy-Item (Join-Path $root "$BuildDirectory\$file") $dest -Force }
$architectures=if($Architecture -eq 'x86'){@('386')}else{@('amd64','386')}
foreach($arch in $architectures) { Copy-Item (Join-Path $root "$CoreDirectory\windows-legacy-$arch") (Join-Path $dest 'cores') -Recurse -Force }
foreach($file in @('README.md','README.ru.md','NOTICE.md','LICENSE')) { Copy-Item (Join-Path $root $file) $dest -Force }
foreach($file in @('compatibility.md','RELEASING.md','HISTORY.ru.md')){Copy-Item (Join-Path $root "docs/$file") (Join-Path $dest 'docs') -Force}
Copy-Item (Join-Path $root 'docs/images/basalt-anonymized.png') (Join-Path $dest 'docs/images') -Force
foreach($file in @('tun-smoke-result.txt','proxy-smoke-result.txt','split-tun-result.txt')) { if(Test-Path (Join-Path $root "$BuildDirectory\$file")) { Copy-Item (Join-Path $root "$BuildDirectory\$file") (Join-Path $dest 'docs') -Force } }
Copy-Item (Join-Path $root 'vendor\YamlDotNet\LICENSE.txt') (Join-Path $dest 'YamlDotNet-LICENSE.txt') -Force
$archive=Join-Path $root ("dist\Basalt-$version-test$suffix.zip")
Compress-Archive -Path $dest -DestinationPath $archive -Force
Get-FileHash $archive -Algorithm SHA256
