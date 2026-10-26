param([string]$FrameworkReferences = 'build\validation\net40\build\.NETFramework\v4.0', [string]$OutputDirectory = 'build')
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
if (!(Test-Path $compiler)) { throw '.NET Framework 4 compiler is required.' }
$output = Join-Path $root $OutputDirectory
New-Item -ItemType Directory -Force $output, (Join-Path $output 'cores') | Out-Null
$core = @(Get-ChildItem (Join-Path $root 'src\Basalt.Core\*.cs') | ForEach-Object FullName)
$desktop = @(Get-ChildItem (Join-Path $root 'src\Basalt.Windows\*.cs') | ForEach-Object FullName)
Push-Location $root
try {
$references = @('System.dll','System.Xml.dll','System.Core.dll','System.Web.Extensions.dll','System.Windows.Forms.dll','System.Drawing.dll','System.Security.dll')
$frameworkArgs = @()
if (Test-Path (Join-Path $FrameworkReferences 'mscorlib.dll')) {
    # Relative paths avoid legacy csc command-line quoting issues in paths containing spaces.
    $referencePath = (Resolve-Path -Relative $FrameworkReferences)
    $frameworkArgs = @('/noconfig','/nostdlib+')
    $references = @('mscorlib.dll') + $references
    foreach ($reference in $references) { $frameworkArgs += '/reference:' + (Join-Path $referencePath $reference) }
    Write-Host 'Targeting .NET Framework 4.0 reference assemblies.'
} else {
    Write-Warning 'Building against installed Framework assemblies; use -FrameworkReferences for strict .NET 4.0 validation.'
    foreach ($reference in $references) { $frameworkArgs += '/reference:' + $reference }
}
if ((Get-FileHash vendor\YamlDotNet\YamlDotNet.dll -Algorithm SHA256).Hash -ne '4636B36DBECCDFA90FBE0ED04A75E1DC6019ED217B7109F5594F954C3E92158A') { throw 'YamlDotNet hash mismatch.' }
Copy-Item vendor\YamlDotNet\YamlDotNet.dll "$OutputDirectory\YamlDotNet.dll" -Force
$frameworkArgs += "/reference:$OutputDirectory\YamlDotNet.dll"
& $compiler /nologo /target:library /resource:src\Basalt.Core\Locales\en.json,Basalt.en.json /optimize+ /warnaserror+ "/out:$OutputDirectory\Basalt.Core.dll" $frameworkArgs $core
if ($LASTEXITCODE -ne 0) { throw 'Core compilation failed.' }
& $compiler /nologo /target:winexe /win32icon:assets\Basalt.ico /resource:assets\Basalt.ico,Basalt.Icon /platform:x86 /optimize+ /warnaserror+ "/out:$OutputDirectory\Basalt.exe" "/reference:$OutputDirectory\Basalt.Core.dll" $frameworkArgs $desktop
if ($LASTEXITCODE -ne 0) { throw 'Desktop compilation failed.' }
Copy-Item (Join-Path $root 'src\Basalt.Windows\App.config') "$output\Basalt.exe.config" -Force
& $compiler /nologo /target:exe "/out:$OutputDirectory\Basalt.Tests.exe" "/reference:$OutputDirectory\Basalt.Core.dll" $frameworkArgs tests\Tests.cs tests\FormatFixtures.cs
if ($LASTEXITCODE -ne 0) { throw 'Test compilation failed.' }
& "$output\Basalt.Tests.exe"
if ($LASTEXITCODE -ne 0) { throw 'Tests failed.' }
& $compiler /nologo /target:exe /platform:x86 "/out:$OutputDirectory\Basalt.UiRegressionTests.exe" "/reference:$OutputDirectory\Basalt.Core.dll" "/reference:$OutputDirectory\Basalt.exe" $frameworkArgs tests\UiRegressionTests.cs
if ($LASTEXITCODE -ne 0) { throw 'UI regression test compilation failed.' }
& "$output\Basalt.UiRegressionTests.exe"
if ($LASTEXITCODE -ne 0) { throw 'UI regression tests failed.' }
Write-Host "Built: $output\Basalt.exe"
} finally { Pop-Location }
