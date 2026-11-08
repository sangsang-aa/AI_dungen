param(
    [string]$GameDir = "D:\steam\steamapps\common\Enter the Gungeon",
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",
    [switch]$NoDeploy
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$proj = Join-Path $root "game\EtgGym\EtgGym.csproj"

if (-not (Test-Path (Join-Path $GameDir "EtG_Data\Managed\Assembly-CSharp.dll"))) {
    throw "Game not found at: $GameDir"
}

Write-Host "Building $proj ($Configuration) ..."
dotnet build $proj -c $Configuration "-p:GameDir=$GameDir"
if ($LASTEXITCODE -ne 0) { throw "dotnet build failed with exit code $LASTEXITCODE" }

$outDll = Join-Path $root "game\EtgGym\bin\$Configuration\EtgGym.dll"
if (-not (Test-Path $outDll)) { throw "Build output missing: $outDll" }

if (-not $NoDeploy) {
    $pluginDir = Join-Path $GameDir "BepInEx\plugins\EtgGym"
    New-Item -ItemType Directory -Force $pluginDir | Out-Null
    Copy-Item $outDll $pluginDir -Force
    $pdb = [System.IO.Path]::ChangeExtension($outDll, ".pdb")
    if (Test-Path $pdb) { Copy-Item $pdb $pluginDir -Force }
    Write-Host "Deployed to: $pluginDir"
}

Write-Host "Done."
