[CmdletBinding()]
param([string]$ScratchDirectory)
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if (-not $ScratchDirectory) {
    $ScratchDirectory = & 'C:\E\kp\aaBenimProgramlarim\NewTemp\NewTemp.exe'
    if ($LASTEXITCODE -ne 0) { throw 'NewTemp could not create a temporary folder.' }
}
$ScratchDirectory = [IO.Path]::GetFullPath($ScratchDirectory)
$buildRoot = Join-Path $ScratchDirectory 'build'
Push-Location $projectRoot
try {
    dotnet publish NewTemp.csproj -c Release -r win-x64 --self-contained true -p:RuntimeFrameworkVersion=10.0.12 -p:NuGetAudit=false "-p:BaseOutputPath=$buildRoot\bin\" "-p:BaseIntermediateOutputPath=$buildRoot\obj\" --ignore-failed-sources -o (Join-Path $ScratchDirectory 'publish\self-contained')
    if ($LASTEXITCODE -ne 0) { throw 'Self-contained publish failed.' }
    dotnet publish NewTemp.csproj -c Release -r win-x64 --self-contained false -p:NuGetAudit=false "-p:BaseOutputPath=$buildRoot\bin\" "-p:BaseIntermediateOutputPath=$buildRoot\obj\" --ignore-failed-sources -o (Join-Path $ScratchDirectory 'publish\framework-dependent')
    if ($LASTEXITCODE -ne 0) { throw 'Framework-dependent publish failed.' }
}
finally { Pop-Location }
