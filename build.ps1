param(
    [string]$GamePath = 'C:\Program Files (x86)\Steam\steamapps\common\Timberborn'
)

$ErrorActionPreference = 'Stop'
$projectRoot = $PSScriptRoot
$projectPath = Join-Path $projectRoot 'src\TimberbornHD\TimberbornHD.csproj'
$modPath = Join-Path $projectRoot 'mod\TimberbornHD'
$distPath = Join-Path $projectRoot 'dist'
$archivePath = Join-Path $distPath 'TimberbornHD.zip'

if (-not (Test-Path -LiteralPath (Join-Path $GamePath 'Timberborn.exe'))) {
    throw "Timberborn was not found at '$GamePath'."
}

dotnet build $projectPath `
    --configuration Release `
    -p:TimberbornGameDir="$GamePath"

if ($LASTEXITCODE -ne 0) {
    throw 'The Timberborn HD build failed.'
}

$assemblyPath = Join-Path $projectRoot 'src\TimberbornHD\bin\Release\netstandard2.1\TimberbornHD.dll'
Copy-Item -LiteralPath $assemblyPath -Destination $modPath -Force

New-Item -ItemType Directory -Path $distPath -Force | Out-Null
Compress-Archive -Path $modPath -DestinationPath $archivePath -Force

Write-Host "Built: $archivePath"

