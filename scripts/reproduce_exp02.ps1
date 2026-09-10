# Reproduce Experiment 02 inside GitHub_mirror
$ErrorActionPreference = "Stop"
$scripts = Split-Path -Parent $MyInvocation.MyCommand.Path
$mirror = Split-Path -Parent $scripts
Set-Location $mirror

$proj = Join-Path $mirror "src\NnPruneHsm\NnPruneHsm.csproj"
$msbuild = "${env:WINDIR}\Microsoft.NET\Framework\v4.0.30319\MSBuild.exe"
if (-not (Test-Path $msbuild)) { Write-Error "MSBuild not found at $msbuild" }
if (-not (Test-Path $proj)) { Write-Error "Missing $proj" }

& $msbuild $proj /p:Configuration=Debug /p:Platform=x86 /v:minimal
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$exe = Join-Path $mirror "src\NnPruneHsm\bin\Debug\NnPruneHsm.exe"
$out = Join-Path $mirror "results\Exp02_PiArea"
& $exe --exp02 --out $out @args
exit $LASTEXITCODE
