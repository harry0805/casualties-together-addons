param(
    [string]$GamePath = 'C:\Program Files (x86)\Steam\steamapps\common\Casualties Unknown Demo',
    [switch]$Deploy
)

$ErrorActionPreference = 'Stop'
$projectPath = Join-Path $PSScriptRoot 'CasualtiesTogetherAddons\CasualtiesTogetherAddons.csproj'
$togetherFiles = @(Get-ChildItem -LiteralPath (Join-Path $GamePath 'BepInEx\plugins') -Filter CasualtiesMP.dll -Recurse -File)
if ($togetherFiles.Count -ne 1) {
    throw "Expected one installed CasualtiesMP.dll; found $($togetherFiles.Count). Check the multiplayer installation."
}

& dotnet build $projectPath -c Release "-p:GamePath=$GamePath" "-p:TogetherDll=$($togetherFiles[0].FullName)"
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }

if ($Deploy) {
    if (Get-Process -Name CasualtiesUnknown -ErrorAction SilentlyContinue) {
        throw 'Close Casualties Unknown before installing the plugin.'
    }
    $pluginDirectory = Join-Path $GamePath 'BepInEx\plugins\CasualtiesTogetherAddons'
    New-Item -ItemType Directory -Path $pluginDirectory -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'CasualtiesTogetherAddons\bin\Release\netstandard2.1\CasualtiesTogetherAddons.dll') -Destination $pluginDirectory -Force
    Write-Host "Installed CasualtiesTogetherAddons.dll in $pluginDirectory"
}
