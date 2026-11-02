param(
    [string]$GamePath = 'C:\Program Files (x86)\Steam\steamapps\common\Casualties Unknown Demo'
)
$ErrorActionPreference = 'Stop'
& dotnet build (Join-Path $PSScriptRoot 'tests\FeatureTests.csproj') -c Release "-p:GamePath=$GamePath"
if ($LASTEXITCODE -ne 0) { throw 'Test build failed.' }
& (Join-Path $PSScriptRoot 'tests\bin\Release\net48\FeatureTests.exe')
if ($LASTEXITCODE -ne 0) { throw 'Feature tests failed.' }
