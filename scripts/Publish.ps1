[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
Push-Location $projectRoot
try {
    dotnet publish NewTemp.csproj -c Release -r win-x64 --self-contained true -p:RuntimeFrameworkVersion=10.0.12 -p:NuGetAudit=false --ignore-failed-sources -o publish\self-contained
    if ($LASTEXITCODE -ne 0) { throw 'Self-contained publish failed.' }
    dotnet publish NewTemp.csproj -c Release -r win-x64 --self-contained false -p:NuGetAudit=false --ignore-failed-sources -o publish\framework-dependent
    if ($LASTEXITCODE -ne 0) { throw 'Framework-dependent publish failed.' }
}
finally { Pop-Location }
